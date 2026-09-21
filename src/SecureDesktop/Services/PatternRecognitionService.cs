using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Services
{
    public class PatternRecognitionService : IDisposable
    {
        private readonly double _defaultMatchThreshold;
        private readonly double _acceptThreshold;
        private readonly int _intervalMs;
        private CancellationTokenSource _cts;
        private Task _worker;
        private bool _isRunning;
        private List<CachedPattern> _patterns;
        private readonly Dictionary<string, Rectangle> _lastLocations = new Dictionary<string, Rectangle>();
        private readonly object _sync = new object();

        // Punkt (0,0) przechwyconej bitmapy odpowiada temu punktowi na "wirtualnym"
        // ekranie (obejmującym wszystkie monitory). Potrzebne, aby poprawnie
        // przenieść znalezione współrzędne na konkretne monitory w konfiguracjach
        // wielo-ekranowych (na monitorze innym niż główny origin bywa ujemny).
        private Point _captureOrigin = Point.Empty;

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

        /// <param name="defaultMatchThreshold">
        /// Próg zgodności używany dla wzorców, które nie mają ustawionego
        /// własnego progu (Pattern.MatchThreshold &lt;= 0). W praktyce każdy
        /// wzorzec ma domyślnie ustawiony własny próg 0.75, więc ta wartość
        /// pełni rolę zabezpieczenia.
        /// </param>
        /// <param name="earlyAcceptThreshold">
        /// Próg, po którego przekroczeniu przeszukiwanie okolicy danego
        /// punktu może zakończyć się wcześniej (optymalizacja wydajności,
        /// nie wpływa na to, czy wzorzec zostanie uznany za znaleziony).
        /// </param>
        /// <param name="intervalMs">
        /// Odstęp między kolejnymi próbami wykrycia wzorców na ekranie.
        /// Wcześniej był zaszyty na sztywno (200 ms) i ignorował ustawienie
        /// "Interwał (ms)" z konfiguracji.
        /// </param>
        public PatternRecognitionService(double defaultMatchThreshold = 0.75, double earlyAcceptThreshold = 0.93, int intervalMs = 200)
        {
            _defaultMatchThreshold = defaultMatchThreshold;
            _acceptThreshold = earlyAcceptThreshold;
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
                        if (p == null)
                            continue;

                        // Pomijamy wzorce oznaczone jako nieaktywne - poprzednio
                        // były one i tak wczytywane, co mogło niepotrzebnie
                        // obciążać wyszukiwanie i maskować to, które wzorce
                        // faktycznie są brane pod uwagę.
                        if (!p.IsActive)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"PatternRecognitionService: pomijam nieaktywny wzorzec '{p.Name}' (Id={p.Id}).");
                            continue;
                        }

                        // Wzorzec bez danych obrazu (np. domyślny placeholder
                        // "Przykladowy wzorzec" tworzony przy pierwszym uruchomieniu
                        // konfiguracji) wcześniej powodował wyjątek już w konstruktorze
                        // CachedPattern. Ponieważ cała lista była budowana jednym
                        // wywołaniem .Select(...).ToList(), pojedynczy wadliwy wzorzec
                        // przerywał całą operację i Start() rzucał wyjątek dalej -
                        // w efekcie ŻADEN wzorzec (także poprawnie dodane) nie był
                        // wyszukiwany. To był najbardziej prawdopodobny powód, dla
                        // którego dodanie drugiego wzorca "psuło" działanie blokady.
                        if (p.ImageData == null || p.ImageData.Length == 0)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"PatternRecognitionService: pomijam wzorzec '{p.Name}' (Id={p.Id}) - brak danych obrazu.");
                            continue;
                        }

                        try
                        {
                            _patterns.Add(new CachedPattern(p, _defaultMatchThreshold));
                        }
                        catch (Exception ex)
                        {
                            // Izolacja błędu per-wzorzec: jeśli jeden wzorzec ma
                            // uszkodzone/nieprawidłowe dane obrazu, pomijamy go,
                            // ale reszta poprawnych wzorców nadal działa.
                            System.Diagnostics.Debug.WriteLine(
                                $"PatternRecognitionService: pomijam wzorzec '{p.Name}' (Id={p.Id}) - błąd wczytywania: {ex.Message}");
                        }
                    }
                }

                if (_patterns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("PatternRecognitionService: brak poprawnych wzorców do wyszukania.");
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
                            List<CachedPattern> patternsSnapshot;
                            lock (_sync) { patternsSnapshot = _patterns; }

                            if (patternsSnapshot != null)
                            {
                                foreach (var pattern in patternsSnapshot)
                                {
                                    if (token.IsCancellationRequested) break;

                                    try
                                    {
                                        ProcessPattern(screen, pattern);
                                    }
                                    catch (Exception exPattern)
                                    {
                                        // Błąd przy jednym wzorcu nie może przerywać
                                        // przetwarzania pozostałych wzorców w tej klatce.
                                        System.Diagnostics.Debug.WriteLine(
                                            $"ProcessPattern error for '{pattern?.Source?.Name}': {exPattern.Message}");
                                    }
                                }
                            }
                        }
                        finally
                        {
                            screen.Dispose();
                        }
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

            Rectangle result = FindPattern(screen, pattern, false);

            if (result == Rectangle.Empty && _lastLocations.ContainsKey(key))
            {
                result = FindPattern(screen, pattern, true);
            }

            System.Diagnostics.Debug.WriteLine(
                $"ProcessPattern: '{key}' -> Found={result != Rectangle.Empty}, " +
                $"Threshold={pattern.Threshold:F2}, Score={pattern.LastScore:F2}, Location={result}");

            if (result != Rectangle.Empty)
            {
                Rectangle prev;
                bool hasPrev = _lastLocations.TryGetValue(key, out prev);
                bool changed;

                if (hasPrev)
                {
                    int dx = Math.Abs(prev.X - result.X);
                    int dy = Math.Abs(prev.Y - result.Y);

                    // Aktualizuj tylko jeśli zmiana jest większa niż 3 piksele
                    if (dx > 3 || dy > 3)
                    {
                        // Płynne wygładzanie - średnia ważona (70% stara, 30% nowa)
                        int smoothX = (prev.X * 7 + result.X * 3) / 10;
                        int smoothY = (prev.Y * 7 + result.Y * 3) / 10;
                        result = new Rectangle(smoothX, smoothY, result.Width, result.Height);
                        changed = true;
                    }
                    else
                    {
                        // Za mała zmiana - ignoruj
                        changed = false;
                    }
                }
                else
                {
                    // Pierwsze wykrycie - zawsze aktualizuj
                    changed = true;
                }

                if (changed)
                {
                    // _lastLocations trzymamy w lokalnych współrzędnych bitmapy
                    // (0-based względem punktu _captureOrigin), żeby wyszukiwanie
                    // okolicznościowe (ExpandRectangle) działało spójnie niezależnie
                    // od tego, na którym monitorze faktycznie leży wzorzec.
                    _lastLocations[key] = result;

                    // Na zewnątrz (do UI/overlayów) wystawiamy już współrzędne
                    // bezwzględne (względem całego układu monitorów), bo to one
                    // są potrzebne do poprawnego umieszczenia "okienka" odblokowania
                    // na właściwym monitorze.
                    var absoluteLocation = new Rectangle(
                        result.X + _captureOrigin.X,
                        result.Y + _captureOrigin.Y,
                        result.Width,
                        result.Height);

                    PatternFound?.Invoke(this, new PatternFoundEventArgs
                    {
                        Pattern = pattern.Source,
                        Location = absoluteLocation,
                        Confidence = pattern.LastScore
                    });
                }
            }
            else if (_lastLocations.ContainsKey(key))
            {
                _lastLocations.Remove(key);
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

            try { if (_worker != null) _worker.Wait(1000); } catch { }

            if (toDispose != null)
            {
                foreach (var p in toDispose) p.Dispose();
            }

            _lastLocations.Clear();
        }

        private Bitmap CaptureScreen()
        {
            try
            {
                // Poprzednio używano wyłącznie Screen.PrimaryScreen.Bounds, co
                // oznaczało, że wzorce znajdujące się na dodatkowym monitorze
                // (w konfiguracji wielo-ekranowej) nigdy nie mogły zostać znalezione.
                // SystemInformation.VirtualScreen obejmuje wszystkie monitory.
                Rectangle bounds = SystemInformation.VirtualScreen;
                Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }

                _captureOrigin = bounds.Location;
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private Rectangle FindPattern(Bitmap screen, CachedPattern pattern, bool forceFullScreen)
        {
            string key = pattern.Source.Name ?? ("pattern_" + pattern.Source.Id);
            Rectangle searchArea = new Rectangle(0, 0, screen.Width, screen.Height);

            if (!forceFullScreen)
            {
                Rectangle last;
                if (_lastLocations.TryGetValue(key, out last))
                {
                    searchArea = ExpandRectangle(last, 250, screen.Size);
                }
            }

            if (searchArea.Width <= pattern.Width || searchArea.Height <= pattern.Height)
                return Rectangle.Empty;

            BitmapData screenData = screen.LockBits(searchArea, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                return FindPatternInLockedData(screenData, searchArea, pattern);
            }
            finally
            {
                screen.UnlockBits(screenData);
            }
        }

        private unsafe Rectangle FindPatternInLockedData(BitmapData screenData, Rectangle searchArea, CachedPattern pattern)
        {
            byte* ptr = (byte*)screenData.Scan0;
            int stride = screenData.Stride;

            int maxX = searchArea.Width - pattern.Width;
            int maxY = searchArea.Height - pattern.Height;
            if (maxX <= 0 || maxY <= 0) return Rectangle.Empty;

            int coarseStep = Math.Max(4, Math.Min(pattern.Width, pattern.Height) / 6);
            int rows = (maxY / coarseStep) + 1;

            var bestPerRow = new RowResult[rows];

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
                bestPerRow[ri] = new RowResult { Score = localBest, X = localX, Y = y };
            });

            RowResult candidate = new RowResult { Score = -1 };
            for (int i = 0; i < bestPerRow.Length; i++)
            {
                if (bestPerRow[i].Score > candidate.Score)
                    candidate = bestPerRow[i];
            }

            // Używamy progu WŁAŚCIWEGO DLA TEGO WZORCA, a nie jednego globalnego -
            // to kluczowa poprawka błędu, w którym drugi (i kolejne) wzorce mogły
            // nigdy nie osiągnąć wspólnego, sztywnego progu 0.75/0.93.
            if (candidate.Score < pattern.Threshold * 0.6)
                return Rectangle.Empty;

            double bestScore = 0;
            int bestX = candidate.X;
            int bestY = candidate.Y;

            int refineRadius = coarseStep;
            int rx0 = Math.Max(0, candidate.X - refineRadius);
            int rx1 = Math.Min(maxX, candidate.X + refineRadius);
            int ry0 = Math.Max(0, candidate.Y - refineRadius);
            int ry1 = Math.Min(maxY, candidate.Y + refineRadius);

            for (int y = ry0; y <= ry1; y++)
            {
                bool done = false;
                for (int x = rx0; x <= rx1; x++)
                {
                    double score = CompareFast(ptr, stride, x, y, pattern.Points);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = x;
                        bestY = y;

                        if (bestScore >= _acceptThreshold)
                        {
                            done = true;
                            break;
                        }
                    }
                }
                if (done) break;
            }

            if (bestScore >= pattern.Threshold)
            {
                pattern.LastScore = bestScore;
                return new Rectangle(searchArea.X + bestX, searchArea.Y + bestY, pattern.Width, pattern.Height);
            }

            pattern.LastScore = bestScore;
            return Rectangle.Empty;
        }

        private unsafe double CompareFast(byte* screen, int stride, int x, int y, PatternPoint[] points)
        {
            int good = 0;

            for (int i = 0; i < points.Length; i++)
            {
                PatternPoint p = points[i];
                int px = x + p.X;
                int py = y + p.Y;

                byte* pixel = screen + ((long)py * stride) + (px * 3);

                byte b = pixel[0];
                byte g = pixel[1];
                byte r = pixel[2];

                int dr = r - p.R;
                int dg = g - p.G;
                int db = b - p.B;
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
            if (_cts != null) _cts.Dispose();
        }

        private struct RowResult
        {
            public double Score;
            public int X;
            public int Y;
        }
    }

    internal class CachedPattern : IDisposable
    {
        public Pattern Source;
        public int Width;
        public int Height;
        public PatternPoint[] Points;
        public PatternPoint[] CoarsePoints;
        public double LastScore;

        /// <summary>
        /// Efektywny próg zgodności dla tego wzorca: własny (Pattern.MatchThreshold),
        /// a jeśli nie ustawiono poprawnej wartości - domyślny przekazany do serwisu.
        /// </summary>
        public double Threshold;

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

                if (Width <= 0 || Height <= 0)
                    throw new InvalidOperationException("Wzorzec ma nieprawidłowe wymiary.");

                int targetPoints = Clamp((Width * Height) / 40, 64, 600);
                int gridSize = (int)Math.Sqrt(targetPoints);
                int stepX = Math.Max(1, Width / gridSize);
                int stepY = Math.Max(1, Height / gridSize);

                Points = SamplePoints(bmp, stepX, stepY, 0);

                int coarseStepX = stepX * 3;
                int coarseStepY = stepY * 3;
                CoarsePoints = SamplePoints(bmp, coarseStepX, coarseStepY, 12);
            }
        }

        private PatternPoint[] SamplePoints(Bitmap bmp, int stepX, int stepY, int minPoints)
        {
            var points = new List<PatternPoint>();

            using (LockedBitmapReader locked = new LockedBitmapReader(bmp))
            {
                for (int y = 0; y < Height; y += stepY)
                {
                    for (int x = 0; x < Width; x += stepX)
                    {
                        Color c = locked.GetPixel(x, y);
                        points.Add(new PatternPoint { X = x, Y = y, R = c.R, G = c.G, B = c.B });
                    }
                }
            }

            if (points.Count < minPoints && stepX > 1)
                return SamplePoints(bmp, Math.Max(1, stepX / 2), Math.Max(1, stepY / 2), 0);

            return points.ToArray();
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public void Dispose() { }
    }

    internal sealed class LockedBitmapReader : IDisposable
    {
        private readonly Bitmap _bmp;
        private readonly BitmapData _data;

        public LockedBitmapReader(Bitmap bmp)
        {
            _bmp = bmp;
            _data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        }

        public unsafe Color GetPixel(int x, int y)
        {
            byte* ptr = (byte*)_data.Scan0;
            byte* p = ptr + (y * _data.Stride) + (x * 3);
            return Color.FromArgb(p[2], p[1], p[0]);
        }

        public void Dispose()
        {
            _bmp.UnlockBits(_data);
        }
    }

    internal struct PatternPoint
    {
        public int X;
        public int Y;
        public byte R;
        public byte G;
        public byte B;
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
