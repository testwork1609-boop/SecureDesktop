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
        private const int RequiredConsecutiveHits = 2;
        private const int RequiredConsecutiveMisses = 4;
        private const int PositionJitterPx = 8;

        // Histereza progowa - gdy pattern jest widoczny, używamy niższego progu.
        private const double HysteresisFactor = 0.80;

        // Fallback pełny skan - gdy score w oknie jest BARDZO niski.
        private const double FullScanFactor = 0.50;

        // Próg "małego ruchu" - do tylu pikseli akceptujemy przesunięcie.
        private const int SmallMovePx = 30;

        // Duży skok jest akceptowany tylko gdy score nowy >= score stary + ten bonus.
        private const double BigJumpScoreBonus = 0.15;

        private readonly double _defaultMatchThreshold;
        private readonly int _intervalMs;
        private CancellationTokenSource _cts;
        private Task _worker;
        private bool _isRunning;
        private List<CachedPattern> _patterns;
        private readonly Dictionary<string, Rectangle> _lastLocations = new Dictionary<string, Rectangle>();
        private readonly Dictionary<string, double> _lastScores = new Dictionary<string, double>();
        private readonly Dictionary<string, double> _bestEver = new Dictionary<string, double>();
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
                _bestEver.Clear();
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
                                " threshold=" + cached.Threshold.ToString("F2"));
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

            double effectiveThreshold = alreadyReportedFound
                ? pattern.Threshold * HysteresisFactor
                : pattern.Threshold;

            Rectangle searchArea = hasPrev
                ? Inflate(prev, 100, screen.Size)
                : new Rectangle(0, 0, screen.Width, screen.Height);

            MatchResult match = FindBestMatch(screen, pattern, searchArea, effectiveThreshold);

            if (!match.Found && hasPrev && pattern.LastScore < pattern.Threshold * FullScanFactor)
            {
                Log("'" + key + "': okno słabe, pełny skan");
                match = FindBestMatch(screen, pattern,
                    new Rectangle(0, 0, screen.Width, screen.Height),
                    pattern.Threshold);
            }

            if (!_bestEver.ContainsKey(key) || pattern.LastScore > _bestEver[key])
                _bestEver[key] = pattern.LastScore;

            DateTime lastLog;
            if (!_lastDiagLog.TryGetValue(key, out lastLog) ||
                (DateTime.Now - lastLog).TotalSeconds >= 1.0)
            {
                int h = _hitStreak.ContainsKey(key) ? _hitStreak[key] : 0;
                int m = _missStreak.ContainsKey(key) ? _missStreak[key] : 0;
                Log("DIAG '" + key + "': best=" + pattern.LastScore.ToString("F3") +
                    " max_ever=" + _bestEver[key].ToString("F3") +
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

                // === Decyzja: zapisać pozycję czy zignorować skok ===
                if (!alreadyReportedFound)
                {
                    // Pierwsze wykrycie - zapisz pozycję.
                    if (hitCount >= RequiredConsecutiveHits)
                    {
                        UpdatePositionAndReport(key, pattern, match);
                        _reportedFound[key] = true;
                    }
                }
                else
                {
                    int dx = Math.Abs(prev.X - match.X);
                    int dy = Math.Abs(prev.Y - match.Y);

                    if (dx <= PositionJitterPx && dy <= PositionJitterPx)
                    {
                        // Brak ruchu - nic nie rób.
                    }
                    else if (dx <= SmallMovePx && dy <= SmallMovePx)
                    {
                        // Mały ruch - zaktualizuj pozycję.
                        UpdatePositionAndReport(key, pattern, match);
                    }
                    else
                    {
                        // Duży skok - akceptuj TYLKO gdy score znacznie lepszy.
                        double prevScore = _lastScores.ContainsKey(key) ? _lastScores[key] : 0;
                        if (match.Score >= prevScore + BigJumpScoreBonus)
                        {
                            Log("'" + key + "': akceptuję skok dx=" + dx + " dy=" + dy +
                                " score " + prevScore.ToString("F3") + " -> " + match.Score.ToString("F3"));
                            UpdatePositionAndReport(key, pattern, match);
                        }
                        else
                        {
                            Log("'" + key + "': ODRZUCAM skok dx=" + dx + " dy=" + dy +
                                " score " + prevScore.ToString("F3") + " -> " + match.Score.ToString("F3"));
                            // Pozycja pozostaje bez zmian - pattern nadal "znaleziony".
                        }
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

                    Log("'" + key + "': ZGUBIONO");
                    var h = PatternLost;
                    if (h != null) h(this, new PatternLostEventArgs { Pattern = pattern.Source });
                }
            }

            _hitStreak[key] = hitCount;
            _missStreak[key] = missCount;
        }

        private void UpdatePositionAndReport(string key, CachedPattern pattern, MatchResult match)
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
                    double s = ComputeScore(ptr, stride, x, y, pattern);
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
                    double s = ComputeScore(ptr, stride, x, y, pattern);
                    if (s > bestScore) { bestScore = s; bestX = x; bestY = y; }
                }

            pattern.LastScore = bestScore;

            if (bestScore >= threshold)
                return new MatchResult { Found = true, X = searchArea.X + bestX, Y = searchArea.Y + bestY, Score = bestScore };
            return MatchResult.NotFound;
        }

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
                    double s = ComputeScore(ptr, stride, x, y, pattern);
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

        private unsafe double ComputeScore(byte* ptr, int stride, int offsetX, int offsetY, CachedPattern pattern)
        {
            const int tolR = 30;
            const int tolG = 30;
            const int tolB = 30;

            int n = pattern.PointX.Length;
            if (n == 0) return 0;

            int good = 0;

            for (int i = 0; i < n; i++)
            {
                int px = offsetX + pattern.PointX[i];
                int py = offsetY + pattern.PointY[i];
                byte* pixel = ptr + (long)py * stride + (px * 3);

                int b = pixel[0];
                int g = pixel[1];
                int r = pixel[2];

                int dr = r - pattern.PointR[i]; if (dr < 0) dr = -dr;
                int dg = g - pattern.PointG[i]; if (dg < 0) dg = -dg;
                int db = b - pattern.PointB[i]; if (db < 0) db = -db;

                if (dr <= tolR && dg <= tolG && db <= tolB)
                    good++;
            }

            return (double)good / n;
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

        public byte[] PointR;
        public byte[] PointG;
        public byte[] PointB;

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
                var rs = new List<byte>();
                var ggs = new List<byte>();
                var bs = new List<byte>();

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
                                xs.Add(x);
                                ys.Add(y);
                                bs.Add(pixel[0]);
                                ggs.Add(pixel[1]);
                                rs.Add(pixel[2]);
                            }
                    }
                }
                finally { bmp.UnlockBits(data); }

                PointX = xs.ToArray();
                PointY = ys.ToArray();
                PointR = rs.ToArray();
                PointG = ggs.ToArray();
                PointB = bs.ToArray();

                int n = PointR.Length;
                double sumG = 0, sumSqG = 0;
                for (int i = 0; i < n; i++)
                {
                    double gv = 0.299 * PointR[i] + 0.587 * PointG[i] + 0.114 * PointB[i];
                    sumG += gv;
                    sumSqG += gv * gv;
                }
                Mean = sumG / n;
                double variance = sumSqG / n - Mean * Mean;
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
