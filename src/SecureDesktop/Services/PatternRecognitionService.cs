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
        private readonly object _sync = new object();
        private Point _captureOrigin = Point.Empty;
        private int _frameCount = 0;

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
                    File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\r\n");
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
                Log("=== START PatternRecognitionService ===");
                Log($"BaseDirectory = {AppDomain.CurrentDomain.BaseDirectory}");
                Log($"VirtualScreen = {SystemInformation.VirtualScreen}");
                Log($"AllScreens = {string.Join(" | ", System.Linq.Enumerable.Select(Screen.AllScreens, s => s.Bounds.ToString()))}");

                if (patterns != null)
                {
                    foreach (var p in patterns)
                    {
                        if (p == null) { Log("Wzorzec null - pomijam"); continue; }
                        if (!p.IsActive) { Log($"'{p.Name}': nieaktywny - pomijam"); continue; }
                        if (p.ImageData == null || p.ImageData.Length == 0)
                        {
                            Log($"'{p.Name}': brak ImageData - pomijam");
                            continue;
                        }

                        try
                        {
                            var cached = new CachedPattern(p, _defaultMatchThreshold);
                            _patterns.Add(cached);
                            Log($"'{p.Name}': OK {cached.Width}x{cached.Height} punkty={cached.PointX.Length} " +
                                $"mean={cached.Mean:F1} std={cached.StdDev:F2} threshold={cached.Threshold:F2}");

                            if (cached.StdDev < 5.0)
                                Log($"!!! '{p.Name}': BARDZO NISKI KONTRAST (std={cached.StdDev:F2}). NCC może nie działać.");
                        }
                        catch (Exception ex)
                        {
                            Log($"'{p.Name}': BŁĄD - {ex.Message}");
                        }
                    }
                }

                Log($"Wzorców załadowanych: {_patterns.Count}");

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
                    _frameCount++;
                    Bitmap screen = CaptureScreen();
                    if (screen == null) { Log("CaptureScreen zwrócił null"); }
                    else
                    {
                        if (_frameCount == 1 || _frameCount == 10)
                            Log($"Klatka #{_frameCount}: screen {screen.Width}x{screen.Height} origin={_captureOrigin}");

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
                                    catch (Exception ex) { Log($"ProcessPattern '{pattern?.Source?.Name}': {ex.Message}"); }
                                }
                            }
                        }
                        finally { screen.Dispose(); }
                    }
                }
                catch (Exception ex) { Log("Loop exception: " + ex.Message); }

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

            Rectangle searchArea = hasPrev
                ? Inflate(prev, 150, screen.Size)
                : new Rectangle(0, 0, screen.Width, screen.Height);

            MatchResult match = FindBestMatch(screen, pattern, searchArea);

            if (!_bestEver.ContainsKey(key) || pattern.LastScore > _bestEver[key])
                _bestEver[key] = pattern.LastScore;

            DateTime lastLog;
            if (!_lastDiagLog.TryGetValue(key, out lastLog) ||
                (DateTime.Now - lastLog).TotalSeconds >= 1.0)
            {
                Log($"DIAG '{key}': best={pattern.LastScore:F3} max_ever={_bestEver[key]:F3} " +
                    $"threshold={pattern.Threshold:F2} found={match.Found}");

                if (match.Found)
                    Log($"  -> ZNALEZIONO @ ({match.X},{match.Y}) score={match.Score:F3}");

                _lastDiagLog[key] = DateTime.Now;
            }

            if (match.Found)
            {
                var newRect = new Rectangle(match.X, match.Y, pattern.Width, pattern.Height);
                bool shouldReport = true;

                if (hasPrev)
                {
                    int dx = Math.Abs(prev.X - match.X);
                    int dy = Math.Abs(prev.Y - match.Y);
                    if (dx <= 3 && dy <= 3) shouldReport = false;

                    if (shouldReport && (dx > 20 || dy > 20))
                    {
                        double prevScore;
                        if (_lastScores.TryGetValue(key, out prevScore) && match.Score < prevScore + 0.03)
                            shouldReport = false;
                    }
                }

                if (shouldReport)
                {
                    _lastLocations[key] = newRect;
                    _lastScores[key] = match.Score;

                    var absLocation = new Rectangle(
                        newRect.X + _captureOrigin.X,
                        newRect.Y + _captureOrigin.Y,
                        newRect.Width,
                        newRect.Height);

                    PatternFound?.Invoke(this, new PatternFoundEventArgs
                    {
                        Pattern = pattern.Source,
                        Location = absLocation,
                        Confidence = match.Score
                    });
                }
            }
            else if (hasPrev)
            {
                _lastLocations.Remove(key);
                _lastScores.Remove(key);
                PatternLost?.Invoke(this, new PatternLostEventArgs { Pattern = pattern.Source });
            }
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

        private MatchResult FindBestMatch(Bitmap screen, CachedPattern pattern, Rectangle searchArea)
        {
            if (searchArea.Width < pattern.Width || searchArea.Height < pattern.Height)
                return MatchResult.NotFound;

            BitmapData data = screen.LockBits(searchArea, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try { return FindBestMatchInArea(data, searchArea, pattern); }
            finally { screen.UnlockBits(data); }
        }

        internal unsafe MatchResult FindBestMatchInArea(BitmapData data, Rectangle searchArea, CachedPattern pattern)
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
                if (s >= pattern.Threshold)
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

            if (bestScore >= pattern.Threshold)
                return new MatchResult { Found = true, X = searchArea.X + bestX, Y = searchArea.Y + bestY, Score = bestScore };
            return MatchResult.NotFound;
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

        /// <summary>
        /// Jednorazowy test wzorca względem aktualnego ekranu. Zwraca score
        /// najlepszego dopasowania, pozycję i screenshot z możliwością
        /// podejrzenia, gdzie algorytm "widzi" wzorzec.
        /// </summary>
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
                    var match = svc.FindBestMatchInArea(data,
                        new Rectangle(0, 0, bmp.Width, bmp.Height), cached);

                    result.Score = match.Score;
                    result.Found = match.Found;
                    result.Location = new Point(match.X, match.Y);
                }
                finally { bmp.UnlockBits(data); }
            }
            catch (Exception ex) { result.Error = ex.Message; }
            return result;
        }

        private struct MatchResult
        {
            public bool Found;
            public int X;
            public int Y;
            public double Score;
            public static MatchResult NotFound => new MatchResult { Found = false };
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
                    throw new InvalidOperationException($"Za mały ({Width}x{Height})");
                if (Width > 1024 || Height > 1024)
                    throw new InvalidOperationException($"Za duży ({Width}x{Height})");

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

    public class PatternTestResult
    {
        public bool Found;
        public double Score;
        public double Threshold;
        public double StdDev;
        public Point Location;
        public int PatternWidth;
        public int PatternHeight;
        public string Error;
        public Bitmap Screenshot;
    }
}
