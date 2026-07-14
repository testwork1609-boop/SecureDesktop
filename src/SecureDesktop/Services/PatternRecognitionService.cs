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
        private readonly double _acceptThreshold;
        private CancellationTokenSource _cts;
        private Task _worker;
        private bool _isRunning;
        private List<CachedPattern> _patterns;
        private readonly Dictionary<int, Rectangle> _lastLocations = new Dictionary<int, Rectangle>();
        private readonly object _sync = new object();

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

        public PatternRecognitionService(double matchThreshold = 0.40, double acceptThreshold = 0.70)
        {
            _matchThreshold = matchThreshold;
            _acceptThreshold = acceptThreshold;
        }

        public void Start(List<Pattern> patterns)
        {
            lock (_sync)
            {
                if (_isRunning) return;

                _patterns = patterns
                    .Where(x => x.IsActive)
                    .Select(x => new CachedPattern(x))
                    .ToList();

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
                                    ProcessPattern(screen, pattern);
                                }
                            }
                        }
                        finally
                        {
                            screen.Dispose();
                        }
                    }
                }
                catch { }

                try { await Task.Delay(200, token); }
                catch (TaskCanceledException) { break; }
            }
        }

        private void ProcessPattern(Bitmap screen, CachedPattern pattern)
        {
            Rectangle result = FindPattern(screen, pattern, false);

            if (result == Rectangle.Empty)
            {
                result = FindPattern(screen, pattern, true);
            }

            if (result != Rectangle.Empty)
            {
                Rectangle prev;
                bool changed = !_lastLocations.TryGetValue(pattern.TrackingKey, out prev) || prev != result;
                _lastLocations[pattern.TrackingKey] = result;

                if (changed)
                {
                    PatternFound?.Invoke(this, new PatternFoundEventArgs
                    {
                        Pattern = pattern.Source,
                        Location = result,
                        Confidence = pattern.LastScore,
                        TrackingKey = pattern.TrackingKey
                    });
                }
            }
            else
            {
                if (_lastLocations.ContainsKey(pattern.TrackingKey))
                {
                    _lastLocations.Remove(pattern.TrackingKey);
                    PatternLost?.Invoke(this, new PatternLostEventArgs
                    {
                        Pattern = pattern.Source,
                        TrackingKey = pattern.TrackingKey
                    });
                }
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
                Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }

                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private Rectangle FindPattern(Bitmap screen, CachedPattern pattern, bool forceFullScreen)
        {
            Rectangle searchArea = new Rectangle(0, 0, screen.Width, screen.Height);

            if (!forceFullScreen)
            {
                Rectangle last;
                if (_lastLocations.TryGetValue(pattern.TrackingKey, out last))
                {
                    searchArea = ExpandRectangle(last, 300, screen.Size);
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

            double bestScore = 0;
            int bestX = 0, bestY = 0;

            int step = Math.Max(4, Math.Min(pattern.Width, pattern.Height) / 8);

            for (int y = 0; y < maxY; y += step)
            {
                for (int x = 0; x < maxX; x += step)
                {
                    double score = CompareFast(ptr, stride, x, y, pattern.Points);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = x;
                        bestY = y;

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
                if ((dr * dr + dg * dg + db * db) < 2500)
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
    }

    internal class CachedPattern : IDisposable
    {
        private static int _nextKey = 0;

        public Pattern Source;
        public int Width;
        public int Height;
        public PatternPoint[] Points;
        public double LastScore;
        public int TrackingKey;

        public CachedPattern(Pattern pattern)
        {
            TrackingKey = Interlocked.Increment(ref _nextKey);
            Source = pattern;

            using (MemoryStream ms = new MemoryStream(pattern.ImageData))
            using (Bitmap bmp = new Bitmap(ms))
            {
                Width = bmp.Width;
                Height = bmp.Height;

                int stepX = Math.Max(1, Width / 6);
                int stepY = Math.Max(1, Height / 6);

                var points = new List<PatternPoint>();
                for (int y = 0; y < Height; y += stepY)
                {
                    for (int x = 0; x < Width; x += stepX)
                    {
                        Color c = bmp.GetPixel(x, y);
                        points.Add(new PatternPoint { X = x, Y = y, R = c.R, G = c.G, B = c.B });
                    }
                }
                Points = points.ToArray();
            }
        }

        public void Dispose() { }
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
        public int TrackingKey { get; set; }
    }

    public class PatternLostEventArgs : EventArgs
    {
        public Pattern Pattern { get; set; }
        public int TrackingKey { get; set; }
    }
}