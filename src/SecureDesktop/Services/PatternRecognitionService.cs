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
    /// <summary>
    /// Rozpoznawanie wzorca oparte na NCC (Normalized Cross-Correlation)
    /// na obrazie w odcieniach szarości.
    ///
    /// Zalety NCC w tym zastosowaniu:
    ///  - Niezmiennicze na zmianę jasności/kontrastu (np. przyciemnienie
    ///    przez overlay blokady nie psuje dopasowania).
    ///  - Odporne na drobne różnice antialiasu między klatkami.
    ///  - Daje wyraźny pik dla prawdziwego dopasowania (>0.95) i wyraźnie
    ///    niższe wartości dla przypadkowych (<0.7), co ułatwia odrzucanie
    ///    fałszywych trafień i stabilizację pozycji ramki.
    /// </summary>
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
        private readonly object _sync = new object();
        private Point _captureOrigin = Point.Empty;

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

        public PatternRecognitionService(double defaultMatchThreshold = 0.85,
                                         double earlyAcceptThreshold = 0.97,
                                         int intervalMs = 200)
        {
            _defaultMatchThreshold = defaultMatchThreshold;
            _intervalMs = Math.Max(50, intervalMs);
        }

        public void Start(List<Pattern> patterns)
        {
            lock (_sync)
            {
                if (_isRunning) return;

                _patterns = new List<CachedPattern>();

                if (patterns != null)
                {
                    foreach (var p in patterns)
                    {
                        if (p == null) continue;
                        if (!p.IsActive) continue;
                        if (p.ImageData == null || p.ImageData.Length == 0) continue;

                        try { _patterns.Add(new CachedPattern(p, _defaultMatchThreshold)); }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"PatternRecognitionService: pomijam '{p.Name}' (Id={p.Id}) - {ex.Message}");
                        }
                    }
                }

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
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine(
                                            $"ProcessPattern '{pattern?.Source?.Name}': {ex.Message}");
                                    }
                                }
                            }
                        }
                        finally { screen.Dispose(); }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("PatternRecognition error: " + ex);
                }

                try { await Task.Delay(_intervalMs, token); }
                catch (TaskCanceledException) { break; }
            }
        }

        private void ProcessPattern(Bitmap screen, CachedPattern pattern)
        {
            string key = pattern.Source.Name ?? ("pattern_" + pattern.Source.Id);

            Rectangle prev;
            bool hasPrev = _lastLocations.TryGetValue(key, out prev);

            // 1) Szukaj najpierw w oknie ±150 px wokół ostatniej pozycji
            //    (szybko, stabilnie, brak skoków między odległymi rejonami).
            Rectangle searchArea = hasPrev
                ? Inflate(prev, 150, screen.Size)
                : new Rectangle(0, 0, screen.Width, screen.Height);

            var match = FindBestMatch(screen, pattern, searchArea);

            // 2) Jeśli nie znaleźliśmy w oknie, spróbuj pełny ekran (fallback).
            if (!match.Found && hasPrev)
            {
                match = FindBestMatch(screen, pattern,
                    new Rectangle(0, 0, screen.Width, screen.Height));
            }

            if (match.Found)
            {
                var newRect = new Rectangle(match.X, match.Y, pattern.Width, pattern.Height);
                bool shouldReport = true;

                if (hasPrev)
                {
                    int dx = Math.Abs(prev.X - match.X);
                    int dy = Math.Abs(prev.Y - match.Y);

                    // Anti-jitter: mikro-ruch < 3 px w obu osiach - ignoruj.
                    if (dx <= 3 && dy <= 3)
                        shouldReport = false;

                    // Histereza: duży skok (>20 px) akceptujemy tylko jeśli
                    // nowy wynik jest istotnie lepszy (żeby nie skakać na
                    // fałszywe dopasowania w innych częściach ekranu).
                    if (shouldReport && (dx > 20 || dy > 20))
                    {
                        double prevScore;
                        if (_lastScores.TryGetValue(key, out prevScore))
                        {
                            if (match.Score < prevScore + 0.03)
                                shouldReport = false;
                        }
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
            catch { return null; }
        }

        private Rectangle FindBestMatch(Bitmap screen, CachedPattern pattern, Rectangle searchArea)
        {
            if (searchArea.Width < pattern.Width || searchArea.Height < pattern.Height)
                return Rectangle.Empty;

            BitmapData data = screen.LockBits(searchArea, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                return FindBestMatchLocked(data, searchArea, pattern);
            }
            finally
            {
                screen.UnlockBits(data);
            }
        }

        private unsafe Rectangle FindBestMatchLocked(BitmapData data, Rectangle searchArea, CachedPattern pattern)
        {
            byte* ptr = (byte*)data.Scan0;
            int stride = data.Stride;

            int maxX = searchArea.Width - pattern.Width;
            int maxY = searchArea.Height - pattern.Height;

            if (maxX < 0 || maxY < 0) return Rectangle.Empty;

            // Jedyna możliwa pozycja - tylko jedna próba.
            if (maxX == 0 && maxY == 0)
            {
                double s = ComputeNCC(ptr, stride, 0, 0, pattern);
                pattern.LastScore = s;
                if (s >= pattern.Threshold)
                    return new Rectangle(searchArea.X, searchArea.Y, pattern.Width, pattern.Height);
                return Rectangle.Empty;
            }

            // Krok zgrubny: małe wzorce - 2 px, większe - proporcjonalny.
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
                    if (s > localBest)
                    {
                        localBest = s;
                        localX = x;
                    }
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

            // Refine: dokładny skan w oknie ±coarseStep wokół najlepszego kandydata.
            int r0x = Math.Max(0, cbX - coarseStep);
            int r1x = Math.Min(maxX, cbX + coarseStep);
            int r0y = Math.Max(0, cbY - coarseStep);
            int r1y = Math.Min(maxY, cbY + coarseStep);

            double bestScore = 0;
            int bestX = cbX, bestY = cbY;

            for (int y = r0y; y <= r1y; y++)
            {
                for (int x = r0x; x <= r1x; x++)
                {
                    double s = ComputeNCC(ptr, stride, x, y, pattern);
                    if (s > bestScore)
                    {
                        bestScore = s;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            pattern.LastScore = bestScore;

            if (bestScore >= pattern.Threshold)
            {
                return new Rectangle(
                    searchArea.X + bestX,
                    searchArea.Y + bestY,
                    pattern.Width,
                    pattern.Height);
            }

            return Rectangle.Empty;
        }

        /// <summary>
        /// NCC między wzorcem (pattern.PointGray) a regionem ekranu zaczynającym
        /// się w (offsetX, offsetY) wewnątrz zablokowanego obszaru.
        /// Wynik w [0, 1]: 1.0 = idealne dopasowanie strukturalne.
        /// </summary>
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

                // Luminancja BT.601
                double gv = 0.299 * pixel[2] + 0.587 * pixel[1] + 0.114 * pixel[0];

                sumS += gv;
                sumSqS += gv * gv;
                sumProd += gv * pattern.PointGray[i];
            }

            double meanS = sumS / n;
            double varS = sumSqS / n - meanS * meanS;
            if (varS <= 1e-4) return 0;   // płaski region - brak dopasowania

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
    }

    internal class CachedPattern : IDisposable
    {
        public Pattern Source;
        public int Width;
        public int Height;

        // Punktowe próbki wzorca (offsety względem 0,0 wzorca)
        public int[] PointX;
        public int[] PointY;
        public double[] PointGray;

        // Statystyki wzorca (do NCC)
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
                throw new InvalidOperationException("Wzorzec nie posiada danych obrazu (ImageData).");

            using (MemoryStream ms = new MemoryStream(pattern.ImageData))
            using (Bitmap bmp = new Bitmap(ms))
            {
                Width = bmp.Width;
                Height = bmp.Height;

                if (Width < 4 || Height < 4)
                    throw new InvalidOperationException($"Wzorzec jest za mały ({Width}x{Height}, min 4x4).");
                if (Width > 512 || Height > 512)
                    throw new InvalidOperationException($"Wzorzec jest za duży ({Width}x{Height}, max 512x512).");

                // Krok próbkowania - celujemy w ~256 punktów.
                int totalPixels = Width * Height;
                const int targetPoints = 256;
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
                        {
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
                }
                finally
                {
                    bmp.UnlockBits(data);
                }

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

                if (StdDev < 3.0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Wzorzec '{pattern.Name}' ma bardzo niski kontrast (StdDev={StdDev:F2}). " +
                        "NCC może być niestabilne - rozważ dodanie wzorca o wyraźniejszych krawędziach.");
                }
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
}
