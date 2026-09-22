using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Services
{
    public class PatternRecognitionService : IDisposable
    {
        // Krótszy wymóg na pojawienie się ramki (szybka reakcja),
        // dłuższy na zniknięcie (stabilność, brak migania).
        private const int RequiredConsecutiveHits = 1;
        private const int RequiredConsecutiveMisses = 8;

        // Anti-jitter: minimalna zmiana pozycji (w px), żeby uznać że pattern
        // faktycznie się przesunął (a nie drga o piksel-dwa).
        private const int PositionJitterPx = 6;

        // Histereza progowa: gdy pattern jest aktualnie "found", używamy
        // niższego progu, żeby drobne fluktuacje NCC nie zgłaszały "lost".
        private const double HysteresisFactor = 0.88;

        private readonly double _defaultMatchThreshold;
        private readonly int _intervalMs;
        private CancellationTokenSource _cts;
        private Task _worker;
        private bool _isRunning;
        private List<CachedPattern> _patterns;
        private readonly Dictionary<string, Rectangle> _lastLocations = new Dictionary<string, Rectangle>();
        private readonly Dictionary<string, double> _lastScores = new Dictionary<string, double>();
        private readonly Dictionary<string, DateTime> _lastDiagLog = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, int> _hitStreak = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _missStreak = new Dictionary<string, int>();
        private readonly Dictionary<string, bool> _reportedFound = new Dictionary<string, bool>();
        private readonly object _sync = new object();
        private Point _captureOrigin = Point.Empty;

        private static readonly object _logLock = new object();

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

        public PatternRecognitionService(double defaultMatchThreshold = 0.80,
                                         double earlyAcceptThreshold = 0.97,
                                         int intervalMs = 200)
        {
            _defaultMatchThreshold = defaultMatchThreshold;
            _intervalMs = Math.Max(50, intervalMs);
        }

        private static void Log(string msg)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var dir = Path.Combine(baseDir, "Logs");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "pattern.log");
                lock (_logLock)
                {
                    File.AppendAllText(path, "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + msg + "\r\n");
                }
            }
            catch { }
        }

        public void Start(List<Pattern> patterns)
        {
            lock (_sync)
            {
                if (_isRunning) return;

                _patterns = new List<CachedPattern>();
                _lastDiagLog.Clear();
                _hitStreak.Clear();
                _missStreak.Clear();
                _reportedFound.Clear();
                _lastLocations.Clear();
                _lastScores.Clear();
                Log("=== START PatternRecognitionService ===");

                if (patterns != null)
                {
                    foreach (var p in patterns)
                    {
                        if (p == null) continue;
                        if (!p.IsActive) continue;
                        if (p.ImageData == null || p.ImageData.Length == 0) continue;

                        try
                        {
                            var cached = new CachedPattern(p, _defaultMatchThreshold);
                            _patterns.Add(cached);
                            Log("'" + p.Name + "': OK " + cached.Width + "x" + cached.Height +
                                " std=" + cached.StdDev.ToString("F2") + " threshold=" + cached.Threshold.ToString("F2"));
                        }
                        catch (Exception ex)
                        {
                            Log("'" + p.Name + "': BŁĄD - " + ex.Message);
                        }
                    }
                }

                Log("Wzorców załadowanych: " + _patterns.Count);

                _cts = new CancellationTokenSource();
                _isRunning = true;

                CancellationToken token = _cts.Token;
                _worker = Task.Run(() => WorkerLoop(token), token);
            }
        }

        private async Task WorkerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Bitmap screen = CaptureScreen();
                    if (screen != null)
                    {
                        try
                        {
                            List<CachedPattern> snapshot;
                            lock (_sync) { snapshot = _patterns; }

                            if (snapshot != null)
                            {
                                foreach (var pattern in snapshot)
                                {
                                    if (token.IsCancellationRequested) break;
                                    try { ProcessPattern(screen, pattern); }
                                    catch (Exception ex) { Log("ProcessPattern: " + ex.Message); }
                                }
                            }
                        }
                        finally { screen.Dispose(); }
                    }
                }
                catch (Exception ex) { Log("Loop: " + ex.Message); }

                try { await Task.Delay(_intervalMs, token); }
                catch (TaskCanceledException) { break; }
            }
            Log("=== STOP PatternRecognitionService ===");
        }

        private void ProcessPattern(Bitmap screen, CachedPattern pattern)
        {
            string key = pattern.Source.Name ?? ("pattern_" + pattern.Source.Id);

            Rectangle prev;
            bool hasPrev = _lastLocations.TryGetValue(key, out prev);
            bool alreadyReportedFound = _reportedFound.ContainsKey(key) && _reportedFound[key];

            // Histereza: gdy pattern jest aktualnie widoczny, używamy niższego
            // progu na utrzymanie - drobne fluktuacje NCC nie powodują miss.
            double effectiveThreshold = alreadyReportedFound
                ? pattern.Threshold * HysteresisFactor
                : pattern.Threshold;

            Rectangle searchArea = hasPrev
                ? Inflate(prev, 150, screen.Size)
                : new Rectangle(0, 0, screen.Width, screen.Height);

            MatchResult match = FindBestMatch(screen, pattern, searchArea, effectiveThreshold);

            // Fallback: pełny skan, jeśli w oknie nic nie znaleziono, a mamy zapamiętaną pozycję.
            if (!match.Found && hasPrev)
            {
                match = FindBestMatch(screen, pattern,
                    new Rectangle(0, 0, screen.Width, screen.Height),
                    effectiveThreshold);
            }

            // Diagnostyka raz na sekundę.
            DateTime lastLog;
            if (!_lastDiagLog.TryGetValue(key, out lastLog) ||
                (DateTime.Now - lastLog).TotalSeconds >= 1.0)
            {
                int h = _hitStreak.ContainsKey(key) ? _hitStreak[key] : 0;
                int m = _missStreak.ContainsKey(key) ? _missStreak[key] : 0;
                Log("DIAG '" + key + "': best=" + pattern.LastScore.ToString("F3") +
                    " eff=" + effectiveThreshold.ToString("F2") +
                    " found=" + match.Found + " hit=" + h + " miss=" + m);
                _lastDiagLog[key] = DateTime.Now;
            }

            int hitCount = _hitStreak.ContainsKey(key) ? _hitStreak[key] : 0;
            int missCount = _missStreak.ContainsKey(key) ? _missStreak[key] : 0;

            if (match.Found)
            {
                hitCount++;
                missCount = 0;

                if (!alreadyReportedFound && hitCount >= RequiredConsecutiveHits)
                {
                    var newRect = new Rectangle(match.X, match.Y, pattern.Width, pattern.Height);
                    _lastLocations[key] = newRect;
                    _lastScores[key] = match.Score;
                    _reportedFound[key] = true;

                    var absLocation = new Rectangle(
                        newRect.X + _captureOrigin.X,
                        newRect.Y + _captureOrigin.Y,
                        newRect.Width,
                        newRect.Height);

                    Log("'" + key + "': ZNALEZIONO @ (" + match.X + "," + match.Y + ") score=" + match.Score.ToString("F3"));
                    var h = PatternFound;
                    if (h != null) h(this, new PatternFoundEventArgs
                    {
                        Pattern = pattern.Source,
                        Location = absLocation,
                        Confidence = match.Score
                    });
                }
                else if (alreadyReportedFound)
                {
                    int dx = Math.Abs(prev.X - match.X);
                    int dy = Math.Abs(prev.Y - match.Y);

                    if (dx > PositionJitterPx || dy > PositionJitterPx)
                    {
                        var newRect = new Rectangle(match.X, match.Y, pattern.Width, pattern.Height);
                        _lastLocations[key] = newRect;
                        _lastScores[key] = match.Score;

                        var absLocation = new Rectangle(
                            newRect.X + _captureOrigin.X,
                            newRect.Y + _captureOrigin.Y,
                            newRect.Width,
                            newRect.Height);

                        var h = PatternFound;
                        if (h != null) h(this, new PatternFoundEventArgs
                        {
                            Pattern = pattern.Source,
                            Location = absLocation,
                            Confidence = match.Score
                        });
                    }
                }
            }
            else
            {
                missCount++;
                hitCount = 0;

                if (alreadyReportedFound && missCount >= RequiredConsecutiveMisses)
                {
                    _lastLocations.Remove(key);
                    _lastScores.Remove(key);
                    _reportedFound[key] = false;

                    Log("'" + key + "': ZGUBIONO (po " + missCount + " miss)");
                    var h = PatternLost;
                    if (h != null) h(this, new PatternLostEventArgs { Pattern = pattern.Source });
                }
            }

            _hitStreak[key] = hitCount;
            _missStreak[key] = missCount;
        }

        public void Stop()
        {
            List<CachedPattern> toDispose = null;
            lock (_sync)
            {
                if (!_isRunning) return;
                _cts.Cancel();
                _isRunning = false;
                toDispose = _patterns;
                _patterns = null;
            }
            try { if (_worker != null) _worker.Wait(1500); } catch { }
            if (toDispose != null)
                foreach (var p in toDispose) p.Dispose();

            _lastLocations.Clear();
            _lastScores.Clear();
            _hitStreak.Clear();
            _missStreak.Clear();
            _reportedFound.Clear();
        }

        private Bitmap CaptureScreen()
        {
            try
            {
                Rectangle bounds = SystemInformation.VirtualScreen;
                Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                _captureOrigin = bounds.Location;
                return bmp;
            }
            catch (Exception ex) { Log("CaptureScreen: " + ex.Message); return null; }
        }

        private MatchResult FindBestMatch(Bitmap screen, CachedPattern pattern, Rectangle searchArea, double threshold)
        {
            if (searchArea.Width < pattern.Width || searchArea.Height < pattern.Height)
                return MatchResult.NotFound;

            BitmapData data = screen.LockBits(searchArea, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try { return FindBestMatchInArea(data, searchArea, pattern, threshold); }
            finally { screen.UnlockBits(data); }
        }

        internal unsafe MatchResult FindBestMatchInArea(BitmapData data, Rectangle searchArea, CachedPattern pattern, double threshold)
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            int maxX = searchArea.Width - pattern.Width;
            int maxY = searchArea.Height - pattern.Height;
            if (maxX < 0 || maxY < 0) return MatchResult.NotFound;

            if (maxX == 0 && maxY == 0)
            {
                double s = ComputeNCC(ptr, stride, 0, 0, pattern);
                pattern.LastScore = s;
                if (s >= threshold)
                    return new MatchResult { Found = true, X = searchArea.X, Y = searchArea.Y, Score = s };
                return MatchResult.NotFound;
            }

            int minDim = Math.Min(pattern.Width, pattern.Height);
            int coarseStep = minDim <= 16 ? 2 : Math.Max(4, minDim / 8);

            int numRows = (maxY / coarseStep) + 1;
            var rowScore = new double[numRows];
            var rowX = new int[numRows];
            var rowY = new int[numRows];

            Parallel.For(0, numRows, ri =>
            {
                int y = ri * coarseStep;
                if (y > maxY) y = maxY;

                double localBest = -1;
                int localX = 0;
                for (int x = 0; x <= maxX; x += coarseStep)
                {
                    double s = ComputeNCC(ptr, stride, x, y, pattern);
                    if (s > localBest) { localBest = s; localX = x; }
                }
                rowScore[ri] = localBest;
                rowX[ri] = localX;
                rowY[ri] = y;
            });

            double coarseBest = -1;
            int cbX = 0, cbY = 0;
            for (int i = 0; i < numRows; i++)
            {
                if (rowScore[i] > coarseBest)
                {
                    coarseBest = rowScore[i];
                    cbX = rowX[i];
                    cbY = rowY[i];
                }
            }

            int r0x = Math.Max(0, cbX - coarseStep);
            int r1x = Math.Min(maxX, cbX + coarseStep);
            int r0y = Math.Max(0, cbY - coarseStep);
            int r1y = Math.Min(maxY, cbY + coarseStep);

            double bestScore = 0;
            int bestX = cbX, bestY = cbY;
            for (int y = r0y; y <= r1y; y++)
                for (int x = r0x; x <= r1x; x++)
                {
                    double s = ComputeNCC(ptr, stride, x, y, pattern);
                    if (s > bestScore) { bestScore = s; bestX = x; bestY = y; }
                }

            pattern.LastScore = bestScore;

            if (bestScore >= threshold)
                return new MatchResult { Found = true, X = searchArea.X + bestX, Y = searchArea.Y + bestY, Score = bestScore };
            return MatchResult.NotFound;
        }

        /// <summary>
        /// Znajduje top-N kandydatów (z NMS).
        /// </summary>
        private unsafe List<PatternCandidate> FindTopCandidates(BitmapData data, Rectangle searchArea,
            CachedPattern pattern, int topN)
        {
            var result = new List<PatternCandidate>();
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            int maxX = searchArea.Width - pattern.Width;
            int maxY = searchArea.Height - pattern.Height;
            if (maxX < 0 || maxY < 0) return result;

            int minDim = Math.Min(pattern.Width, pattern.Height);
            int coarseStep = Math.Max(4, minDim / 8);

            var candidates = new List<PatternCandidate>();

            for (int y = 0; y <= maxY; y += coarseStep)
            {
                for (int x = 0; x <= maxX; x += coarseStep)
                {
                    double s = ComputeNCC(ptr, stride, x, y, pattern);
                    candidates.Add(new PatternCandidate
                    {
                        X = searchArea.X + x,
                        Y = searchArea.Y + y,
                        Width = pattern.Width,
                        Height = pattern.Height,
                        Score = s
                    });
                }
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            int minDist = Math.Min(pattern.Width, pattern.Height);
            foreach (var c in candidates)
            {
                bool tooClose = false;
                foreach (var r in result)
                {
                    int dx = Math.Abs(c.X - r.X);
                    int dy = Math.Abs(c.Y - r.Y);
                    if (dx < minDist && dy < minDist) { tooClose = true; break; }
                }
                if (tooClose) continue;
                result.Add(c);
                if (result.Count >= topN) break;
            }

            return result;
        }

        private unsafe double ComputeNCC(byte* ptr, int stride, int offsetX, int offsetY, CachedPattern pattern)
        {
            int n = pattern.PointX.Length;
            double sumS = 0, sumSqS = 0, sumProd = 0;
            double tMean = pattern.Mean;

            for (int i = 0; i < n; i++)
            {
                int px = offsetX + pattern.PointX[i];
                int py = offsetY + pattern.PointY[i];
                byte* pixel = ptr + (long)py * stride + (px * 3);
                double gv = 0.299 * pixel[2] + 0.587 * pixel[1] + 0.114 * pixel[0];

                sumS += gv;
                sumSqS += gv * gv;
                sumProd += gv * pattern.PointGray[i];
            }

            double meanS = sumS / n;
            double varS = sumSqS / n - meanS * meanS;
            if (varS <= 1e-4) return 0;

            double numerator = sumProd / n - meanS * tMean;
            double denom = Math.Sqrt(varS) * pattern.StdDev;
            if (denom <= 1e-6) return 0;

            double ncc = numerator / denom;
            return ncc < 0 ? 0 : ncc;
        }

        private static Rectangle Inflate(Rectangle r, int size, Size screen)
        {
            int x = Math.Max(0, r.X - size);
            int y = Math.Max(0, r.Y - size);
            int right = Math.Min(screen.Width, r.Right + size);
            int bottom = Math.Min(screen.Height, r.Bottom + size);
            if (right <= x || bottom <= y) return new Rectangle(0, 0, screen.Width, screen.Height);
            return new Rectangle(x, y, right - x, bottom - y);
        }

        public void Dispose()
        {
            Stop();
            if (_cts != null) _cts.Dispose();
        }

        public static PatternTestResult TestPatternAgainstCurrentScreen(Pattern pattern, double defaultThreshold = 0.75)
        {
            var result = new PatternTestResult();
            try
            {
                var cached = new CachedPattern(pattern, defaultThreshold);
                result.Threshold = cached.Threshold;
                result.PatternWidth = cached.Width;
                result.PatternHeight = cached.Height;
                result.StdDev = cached.StdDev;

                Rectangle bounds = SystemInformation.VirtualScreen;
                var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                result.Screenshot = bmp;

                BitmapData data = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);

                try
                {
                    var svc = new PatternRecognitionService();
                    var candidates = svc.FindTopCandidates(data,
                        new Rectangle(0, 0, bmp.Width, bmp.Height), cached, 3);

                    result.Candidates = candidates;

                    if (candidates.Count > 0)
                    {
                        result.Score = candidates[0].Score;
                        result.Found = candidates[0].Score >= cached.Threshold;
                        result.Location = new Point(candidates[0].X, candidates[0].Y);
                        result.Crop1 = CropSafe(bmp, candidates[0]);
                    }
                    if (candidates.Count > 1)
                    {
                        result.SecondScore = candidates[1].Score;
                        result.Crop2 = CropSafe(bmp, candidates[1]);
                    }
                    if (candidates.Count > 2)
                    {
                        result.ThirdScore = candidates[2].Score;
                        result.Crop3 = CropSafe(bmp, candidates[2]);
                    }
                }
                finally { bmp.UnlockBits(data); }
            }
            catch (Exception ex) { result.Error = ex.Message; }
            return result;
        }

        private static Bitmap CropSafe(Bitmap src, PatternCandidate c)
        {
            try
            {
                var rect = new Rectangle(c.X, c.Y, c.Width, c.Height);
                if (rect.X < 0) rect.X = 0;
                if (rect.Y < 0) rect.Y = 0;
                if (rect.Right > src.Width) rect.Width = src.Width - rect.X;
                if (rect.Bottom > src.Height) rect.Height = src.Height - rect.Y;
                if (rect.Width <= 0 || rect.Height <= 0) return null;
                return src.Clone(rect, src.PixelFormat);
            }
            catch { return null; }
        }

        internal struct MatchResult
        {
            public bool Found;
            public int X;
            public int Y;
            public double Score;
            public static MatchResult NotFound { get { return new MatchResult { Found = false }; } }
        }
    }

    internal class CachedPattern : IDisposable
    {
        public Pattern Source;
        public int Width;
        public int Height;
        public int[] PointX;
        public int[] PointY;
        public double[] PointGray;
        public double Mean;
        public double StdDev;
        public double Threshold;
        public double LastScore;

        public CachedPattern(Pattern pattern, double defaultThreshold)
        {
            Source = pattern;
            Threshold = (pattern.MatchThreshold > 0 && pattern.MatchThreshold <= 1.0)
                ? pattern.MatchThreshold
                : defaultThreshold;

            if (pattern.ImageData == null || pattern.ImageData.Length == 0)
                throw new InvalidOperationException("Brak ImageData");

            using (MemoryStream ms = new MemoryStream(pattern.ImageData))
            using (Bitmap bmp = new Bitmap(ms))
            {
                Width = bmp.Width;
                Height = bmp.Height;

                if (Width < 4 || Height < 4)
                    throw new InvalidOperationException("Za mały (" + Width + "x" + Height + ")");
                if (Width > 1024 || Height > 1024)
                    throw new InvalidOperationException("Za duży (" + Width + "x" + Height + ")");

                int totalPixels = Width * Height;
                const int targetPoints = 400;
                int step = (int)Math.Round(Math.Sqrt((double)totalPixels / targetPoints));
                if (step < 1) step = 1;

                var xs = new List<int>();
                var ys = new List<int>();
                var gs = new List<double>();

                BitmapData data = bmp.LockBits(
                    new Rectangle(0, 0, Width, Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);

                try
                {
                    unsafe
                    {
                        byte* p = (byte*)data.Scan0;
                        int stride = data.Stride;
                        for (int y = 0; y < Height; y += step)
                            for (int x = 0; x < Width; x += step)
                            {
                                byte* pixel = p + (long)y * stride + (x * 3);
                                double gv = 0.299 * pixel[2] + 0.587 * pixel[1] + 0.114 * pixel[0];
                                xs.Add(x);
                                ys.Add(y);
                                gs.Add(gv);
                            }
                    }
                }
                finally { bmp.UnlockBits(data); }

                PointX = xs.ToArray();
                PointY = ys.ToArray();
                PointGray = gs.ToArray();

                int n = PointGray.Length;
                double sum = 0, sumSq = 0;
                for (int i = 0; i < n; i++)
                {
                    sum += PointGray[i];
                    sumSq += PointGray[i] * PointGray[i];
                }

                Mean = sum / n;
                double variance = sumSq / n - Mean * Mean;
                StdDev = Math.Sqrt(Math.Max(0.01, variance));
            }
        }

        public void Dispose() { }
    }

    public class PatternFoundEventArgs : EventArgs
    {
        public Pattern Pattern { get; set; }
        public Rectangle Location { get; set; }
        public double Confidence { get; set; }
    }

    public class PatternLostEventArgs : EventArgs
    {
        public Pattern Pattern { get; set; }
    }

    public class PatternCandidate
    {
        public int X, Y, Width, Height;
        public double Score;
    }

    public class PatternTestResult
    {
        public bool Found;
        public double Score;
        public double SecondScore;
        public double ThirdScore;
        public double Threshold;
        public double StdDev;
        public Point Location;
        public int PatternWidth;
        public int PatternHeight;
        public string Error;
        public Bitmap Screenshot;
        public Bitmap Crop1;
        public Bitmap Crop2;
        public Bitmap Crop3;
        public List<PatternCandidate> Candidates = new List<PatternCandidate>();
    }
}
