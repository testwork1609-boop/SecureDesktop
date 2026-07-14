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
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private List<CachedPattern> _patterns;
        private readonly Dictionary<int, Rectangle> _lastLocations;

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

        public PatternRecognitionService(double matchThreshold = 0.30)
        {
            _matchThreshold = matchThreshold;
            _lastLocations = new Dictionary<int, Rectangle>();
        }

        public void Start(List<Pattern> patterns)
        {
            if (_isRunning) return;

            _patterns = patterns
                .Where(x => x.IsActive)
                .Take(2)
                .Select(x => new CachedPattern(x))
                .ToList();

            _cts = new CancellationTokenSource();
            _isRunning = true;

            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        using (Bitmap screen = CaptureScreen())
                        {
                            if (screen != null)
                            {
                                foreach (var pattern in _patterns)
                                {
                                    if (_cts.Token.IsCancellationRequested) break;

                                    Rectangle result = FindPattern(screen, pattern);

                                    if (result != Rectangle.Empty)
                                    {
                                        bool changed = !_lastLocations.ContainsKey(pattern.Source.Id) ||
                                            _lastLocations[pattern.Source.Id] != result;

                                        _lastLocations[pattern.Source.Id] = result;

                                        if (changed)
                                        {
                                            PatternFound?.Invoke(this, new PatternFoundEventArgs
                                            {
                                                Pattern = pattern.Source,
                                                Location = result,
                                                Confidence = 1.0
                                            });
                                        }
                                    }
                                    else
                                    {
                                        if (_lastLocations.ContainsKey(pattern.Source.Id))
                                        {
                                            _lastLocations.Remove(pattern.Source.Id);
                                            PatternLost?.Invoke(this, new PatternLostEventArgs
                                            {
                                                Pattern = pattern.Source
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        await Task.Delay(200, _cts.Token);
                    }
                    catch { }
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _cts.Cancel();
            _isRunning = false;

            if (_patterns != null)
            {
                foreach (var p in _patterns)
                    p.Dispose();
                _patterns = null;
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

        private Rectangle FindPattern(Bitmap screen, CachedPattern pattern)
        {
            Rectangle searchArea;

            if (_lastLocations.TryGetValue(pattern.Source.Id, out Rectangle last))
            {
                searchArea = ExpandRectangle(last, 250, screen.Size);
            }
            else
            {
                searchArea = new Rectangle(0, 0, screen.Width, screen.Height);
            }

            BitmapData screenData = screen.LockBits(
                searchArea,
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                unsafe
                {
                    byte* ptr = (byte*)screenData.Scan0;
                    int stride = screenData.Stride;

                    double bestScore = 0;
                    int bestX = 0, bestY = 0;

                    int step = 6;

                    for (int y = 0; y < searchArea.Height - pattern.Height; y += step)
                    {
                        for (int x = 0; x < searchArea.Width - pattern.Width; x += step)
                        {
                            double score = CompareFast(ptr, stride, x, y, pattern);

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestX = x;
                                bestY = y;

                                if (bestScore >= 0.80)
                                    goto Found;
                            }
                        }
                    }

                    Found:
                    if (bestScore >= _matchThreshold)
                    {
                        int realX = searchArea.X + bestX;
                        int realY = searchArea.Y + bestY;

                        return new Rectangle(realX, realY, pattern.Width, pattern.Height);
                    }
                }
            }
            finally
            {
                screen.UnlockBits(screenData);
            }

            return Rectangle.Empty;
        }

        private unsafe double CompareFast(byte* screen, int stride, int x, int y, CachedPattern pattern)
        {
            int good = 0;

            foreach (PatternPoint p in pattern.Points)
            {
                int px = x + p.X;
                int py = y + p.Y;

                if (px >= 0 && py >= 0)
                {
                    byte* pixel = screen + (py * stride) + (px * 3);

                    byte b = pixel[0];
                    byte g = pixel[1];
                    byte r = pixel[2];

                    if (ColorDistance(r, g, b, p.R, p.G, p.B))
                    {
                        good++;
                    }
                }
            }

            return (double)good / pattern.Points.Length;
        }

        private bool ColorDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            int dr = r1 - r2;
            int dg = g1 - g2;
            int db = b1 - b2;

            return (dr * dr + dg * dg + db * db) < 1200;
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
        public Pattern Source;
        public int Width;
        public int Height;
        public PatternPoint[] Points;

        public CachedPattern(Pattern pattern)
        {
            Source = pattern;

            using (MemoryStream ms = new MemoryStream(pattern.ImageData))
            using (Bitmap bmp = new Bitmap(ms))
            {
                Width = bmp.Width;
                Height = bmp.Height;

                var points = new List<PatternPoint>();

                // ZAWSZE 8 kroków, niezależnie od rozmiaru
                int stepX = Math.Max(1, Width / 8);
                int stepY = Math.Max(1, Height / 8);

                for (int y = 0; y < Height; y += stepY)
                {
                    for (int x = 0; x < Width; x += stepX)
                    {
                        Color c = bmp.GetPixel(x, y);
                        points.Add(new PatternPoint
                        {
                            X = x,
                            Y = y,
                            R = c.R,
                            G = c.G,
                            B = c.B
                        });
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
    }

    public class PatternLostEventArgs : EventArgs
    {
        public Pattern Pattern { get; set; }
    }
}