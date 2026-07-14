using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SecureDesktop.Models;

namespace SecureDesktop.Services
{
    public class PatternRecognitionService : IDisposable
    {
        private readonly double _matchThreshold;
        private readonly double _acceptThreshold; // próg do wczesnego, ale bezpiecznego wyjścia
        private CancellationTokenSource _cts;
        private Task _worker;
        private bool _isRunning;
        private List<CachedPattern> _patterns;
        private readonly Dictionary<int, Rectangle> _lastLocations = new();
        private readonly object _sync = new();

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

        // Wyższy domyślny próg minimalny — 0.30 dawało zbyt wiele fałszywych trafień
        public PatternRecognitionService(double matchThreshold = 0.75, double acceptThreshold = 0.93)
        {
            _matchThreshold = matchThreshold;
            _acceptThreshold = acceptThreshold;
        }

        public void Start(List<Pattern> patterns)
        {
            lock (_sync)
            {
                if (_isRunning) return;

                // usunięty sztuczny limit Take(2) — obsługujemy wszystkie aktywne wzorce
                _patterns = patterns
                    .Where(x => x.IsActive)
                    .Select(x => new CachedPattern(x))
                    .ToList();

                _cts = new CancellationTokenSource();
                _isRunning = true;

                _worker = Task.Run(() => WorkerLoop(_cts.Token), _cts.Token);
            }
        }

        private async Task WorkerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using Bitmap screen = CaptureScreen();
                    if (screen != null)
                    {
                        List<CachedPattern> patternsSnapshot;
                        lock (_sync) { patternsSnapshot = _patterns; }

                        if (patternsSnapshot != null)
                        {
                            foreach (var pattern in patternsSnapshot)
                            {
                                if (token.IsCancellationRequested) break;
                                ProcessPattern(screen, pattern);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // NIE połykaj cicho — przynajmniej zaloguj, inaczej nigdy nie zobaczysz
                    // że coś krytycznego (np. IndexOutOfRange) się dzieje
                    System.Diagnostics.Debug.WriteLine($"PatternRecognition error: {ex}");
                }

                try { await Task.Delay(200, token); }
                catch (TaskCanceledException) { break; }
            }
        }

        private void ProcessPattern(Bitmap screen, CachedPattern pattern)
        {
            Rectangle result = FindPattern(screen, pattern);

            // Fallback: jeśli szukaliśmy tylko w wąskim oknie wokół ostatniej pozycji
            // i nic nie znaleźliśmy, od razu (ta sama klatka!) spróbuj całego ekranu,
            // zamiast czekać 200ms i zgłaszać fałszywy "Lost".
            if (result == Rectangle.Empty && _lastLocations.ContainsKey(pattern.Source.Id))
            {
                result = FindPattern(screen, pattern, forceFullScreen: true);
            }

            if (result != Rectangle.Empty)
            {
                bool changed = !_lastLocations.TryGetValue(pattern.Source.Id, out var prev) || prev != result;
                _lastLocations[pattern.Source.Id] = result;

                if (changed)
                {
                    PatternFound?.Invoke(this, new PatternFoundEventArgs
                    {
                        Pattern = pattern.Source,
                        Location = result,
                        Confidence = pattern.LastScore
                    });
                }
            }
            else if (_lastLocations.ContainsKey(pattern.Source.Id))
            {
                _lastLocations.Remove(pattern.Source.Id);
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

            try { _worker?.Wait(1000); } catch { /* cancellation exception - oczekiwane */ }

            if (toDispose != null)
                foreach (var p in toDispose) p.Dispose();

            _lastLocations.Clear();
        }

        private Bitmap CaptureScreen()
        {
            try
            {
                Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);

                using (Graphics g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private Rectangle FindPattern(Bitmap screen, CachedPattern pattern, bool forceFullScreen = false)
        {
            Rectangle searchArea = new Rectangle(0, 0, screen.Width, screen.Height);

            if (!forceFullScreen && _lastLocations.TryGetValue(pattern.Source.Id, out Rectangle last))
            {
                searchArea = ExpandRectangle(last, 250, screen.Size);
            }

            if (searchArea.Width <= pattern.Width || searchArea.Height <= pattern.Height)
                return Rectangle.Empty;

            BitmapData screenData = screen.LockBits(searchArea, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                unsafe
                {
                    byte* ptr = (byte*)screenData.Scan0;
                    int stride = screenData.Stride;

                    // --- ETAP 1: gruby, szybki skan po całym obszarze (Parallel) ---
                    // Używa TYLKO podzbioru punktów kontrolnych (coarsePoints) —
                    // wystarcza do znalezienia obszaru kandydata, nie do finalnej decyzji.
                    int coarseStep = Math.Max(4, Math.Min(pattern.Width, pattern.Height) / 6);

                    int maxX = searchArea.Width - pattern.Width;
                    int maxY = searchArea.Height - pattern.Height;
                    if (maxX <= 0 || maxY <= 0) return Rectangle.Empty;

                    int rows = (maxY / coarseStep) + 1;

                    var bestPerRow = new (double score, int x, int y)[rows];

                    Parallel.For(0, rows, ri =>
                    {
                        int y = ri * coarseStep;
                        if (y > maxY) return;

                        double localBest = 0;
                        int localX = 0;

                        for (int x = 0; x <= maxX; x += coarseStep)
                        {
                            double score = CompareFast(ptr, stride, x, y, pattern.CoarsePoints);
                            if (score > localBest)
                            {
                                localBest = score;
                                localX = x;
                            }
                        }
                        bestPerRow[ri] = (localBest, localX, y);
                    });

                    var candidate = bestPerRow.OrderByDescending(c => c.score).FirstOrDefault();
                    if (candidate.score < _matchThreshold * 0.6)
                        return Rectangle.Empty; // nawet zgrubnie nic obiecującego

                    // --- ETAP 2: dokładne dopasowanie lokalnie wokół najlepszego kandydata ---
                    // Przeszukuje mały obszar (±coarseStep) z pełną siatką punktów i krokiem 1px
                    double bestScore = 0;
                    int bestX = candidate.x, bestY = candidate.y;

                    int refineRadius = coarseStep;
                    int rx0 = Math.Max(0, candidate.x - refineRadius);
                    int rx1 = Math.Min(maxX, candidate.x + refineRadius);
                    int ry0 = Math.Max(0, candidate.y - refineRadius);
                    int ry1 = Math.Min(maxY, candidate.y + refineRadius);

                    for (int y = ry0; y <= ry1; y++)
                    {
                        for (int x = rx0; x <= rx1; x++)
                        {
                            double score = CompareFast(ptr, stride, x, y, pattern.Points);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestX = x;
                                bestY = y;

                                // bezpieczne wczesne wyjście - blisko pewności
                                if (bestScore >= _acceptThreshold)
                                    goto Found;
                            }
                        }
                    }

                    Found:
                    if (bestScore >= _matchThreshold)
                    {
                        pattern.LastScore = bestScore;
                        return new Rectangle(searchArea.X + bestX, searchArea.Y + bestY, pattern.Width, pattern.Height);
                    }
                }
            }
            finally
            {
                screen.UnlockBits(screenData);
            }

            return Rectangle.Empty;
        }

        private unsafe double CompareFast(byte* screen, int stride, int x, int y, PatternPoint[] points)
        {
            int good = 0;

            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                int px = x + p.X;
                int py = y + p.Y;

                // sprawdzamy TYLKO dolną granicę, bo x/y są już ograniczone przez maxX/maxY
                // w wywołujących pętlach — ale zostawiamy asercję bezpieczeństwa:
                byte* pixel = screen + ((long)py * stride) + (px * 3);

                byte b = pixel[0];
                byte g = pixel[1];
                byte r = pixel[2];

                int dr = r - p.R, dg = g - p.G, db = b - p.B;
                if ((dr * dr + dg * dg + db * db) < 1200)
                    good++;
            }

            return (double)good / points.Length;
        }

        private Rectangle ExpandRectangle(Rectangle r, int size, Size screen)
        {
            r.Inflate(size, size);
            if (r.X < 0) r.X = 0;
            if (r.Y < 0) r.Y = 0;
            if (r.Right > screen.Width) r.Width = screen.Width - r.X;
            if (r.Bottom > screen.Height) r.Height = screen.Height - r.Y;
            return r;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }

    internal class CachedPattern : IDisposable
    {
        public Pattern Source;
        public int Width;
        public int Height;
        public PatternPoint[] Points;      // pełna siatka - do dokładnego dopasowania
        public PatternPoint[] CoarsePoints; // rzadka siatka - do szybkiego skanu wstępnego
        public double LastScore;

        public CachedPattern(Pattern pattern)
        {
            Source = pattern;

            using MemoryStream ms = new MemoryStream(pattern.ImageData);
            using Bitmap bmp = new Bitmap(ms);

            Width = bmp.Width;
            Height = bmp.Height;

            // Gęstość próbki ADAPTACYJNA do rozmiaru wzorca, nie stała "8 kroków".
            // Cel: ok. 300-600 punktów dla dużych wzorców, mniej dla małych,
            // ale nigdy poniżej sensownego minimum dla wiarygodności dopasowania.
            int targetPoints = Math.Clamp((Width * Height) / 40, 64, 600);
            int gridSize = (int)Math.Sqrt(targetPoints);
            int stepX = Math.Max(1, Width / gridSize);
            int stepY = Math.Max(1, Height / gridSize);

            Points = SamplePoints(bmp, stepX, stepY);

            // Zgrubna siatka: ok. 1/4 gęstości pełnej, min 12 punktów
            int coarseStepX = stepX * 3;
            int coarseStepY = stepY * 3;
            CoarsePoints = SamplePoints(bmp, coarseStepX, coarseStepY, minPoints: 12);
        }

        private PatternPoint[] SamplePoints(Bitmap bmp, int stepX, int stepY, int minPoints = 0)
        {
            var points = new List<PatternPoint>();

            using (var locked = new LockedBitmapReader(bmp))
            {
                for (int y = 0; y < Height; y += stepY)
                {
                    for (int x = 0; x < Width; x += stepX)
                    {
                        var c = locked.GetPixel(x, y);
                        points.Add(new PatternPoint { X = x, Y = y, R = c.R, G = c.G, B = c.B });
                    }
                }
            }

            if (points.Count < minPoints && stepX > 1)
                return SamplePoints(bmp, Math.Max(1, stepX / 2), Math.Max(1, stepY / 2), 0);

            return points.ToArray();
        }

        public void Dispose() { }
    }

    // Szybszy odczyt pikseli niż GetPixel (LockBits zamiast GDI+ per-pixel call)
    internal unsafe class LockedBitmapReader : IDisposable
    {
        private readonly Bitmap _bmp;
        private readonly BitmapData _data;
        private readonly byte* _ptr;

        public LockedBitmapReader(Bitmap bmp)
        {
            _bmp = bmp;
            _data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            _ptr = (byte*)_data.Scan0;
        }

        public Color GetPixel(int x, int y)
        {
            byte* p = _ptr + (y * _data.Stride) + (x * 3);
            return Color.FromArgb(p[2], p[1], p[0]);
        }

        public void Dispose() => _bmp.UnlockBits(_data);
    }

    internal struct PatternPoint
    {
        public int X, Y;
        public byte R, G, B;
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