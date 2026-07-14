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
        private readonly Dictionary<string, Rectangle> _lastLocations = new Dictionary<string, Rectangle>();
        private readonly object _sync = new object();

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("PatternRecognition error: " + ex);
                }

                try { await Task.Delay(200, token); }
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

            if (result != Rectangle.Empty)
            {
                Rectangle prev;
                bool changed = !_lastLocations.TryGetValue(key, out prev) || prev != result;
                _lastLocations[key] = result;

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

            if (candidate.Score < _matchThreshold * 0.6)
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

        public CachedPattern(Pattern pattern)
        {
            Source = pattern;

            using (MemoryStream ms = new MemoryStream(pattern.ImageData))
            using (Bitmap bmp = new Bitmap(ms))
            {
                Width = bmp.Width;
                Height = bmp.Height;

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