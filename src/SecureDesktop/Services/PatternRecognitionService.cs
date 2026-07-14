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
        private List<Pattern> _currentPatterns;
        private Dictionary<int, Rectangle> _lastFoundLocations;

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

        public PatternRecognitionService(double matchThreshold = 0.15)
        {
            _matchThreshold = matchThreshold;
            _lastFoundLocations = new Dictionary<int, Rectangle>();
        }

        public void Start(List<Pattern> patterns)
        {
            if (_isRunning) return;
            
            _currentPatterns = patterns;
            _lastFoundLocations.Clear();
            _cts = new CancellationTokenSource();
            _isRunning = true;

            Task.Run(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        using (var screenshot = CaptureScreen())
                        {
                            if (screenshot != null && _currentPatterns != null)
                            {
                                foreach (var pattern in _currentPatterns.Where(p => p.IsActive))
                                {
                                    if (_cts.Token.IsCancellationRequested) break;
                                    
                                    var location = FindPatternFast(screenshot, pattern);
                                    
                                    if (location != Rectangle.Empty)
                                    {
                                        if (!_lastFoundLocations.ContainsKey(pattern.Id) ||
                                            _lastFoundLocations[pattern.Id] != location)
                                        {
                                            _lastFoundLocations[pattern.Id] = location;
                                            
                                            PatternFound?.Invoke(this, new PatternFoundEventArgs
                                            {
                                                Pattern = pattern,
                                                Location = location,
                                                Confidence = 1.0
                                            });
                                        }
                                    }
                                    else
                                    {
                                        if (_lastFoundLocations.ContainsKey(pattern.Id))
                                        {
                                            _lastFoundLocations.Remove(pattern.Id);
                                            PatternLost?.Invoke(this, new PatternLostEventArgs { Pattern = pattern });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    
                    Thread.Sleep(100);
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            _isRunning = false;
            _currentPatterns = null;
            _lastFoundLocations.Clear();
        }

        private Bitmap CaptureScreen()
        {
            try
            {
                var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                }
                return bitmap;
            }
            catch { return null; }
        }

        private Rectangle FindPatternFast(Bitmap screenshot, Pattern pattern)
        {
            try
            {
                if (pattern.ImageData == null || pattern.ImageData.Length == 0)
                    return Rectangle.Empty;

                using (var ms = new MemoryStream(pattern.ImageData))
                using (var patternBmp = new Bitmap(ms))
                {
                    int pw = patternBmp.Width;
                    int ph = patternBmp.Height;
                    
                    if (pw < 5 || ph < 5) return Rectangle.Empty;
                    if (pw > screenshot.Width || ph > screenshot.Height) return Rectangle.Empty;

                    // 5 punktów charakterystycznych
                    Color[] patternColors = new Color[5];
                    Point[] patternPoints = new Point[5];
                    
                    patternPoints[0] = new Point(0, 0);
                    patternPoints[1] = new Point(pw - 1, 0);
                    patternPoints[2] = new Point(pw / 2, ph / 2);
                    patternPoints[3] = new Point(0, ph - 1);
                    patternPoints[4] = new Point(pw - 1, ph - 1);

                    for (int i = 0; i < 5; i++)
                        patternColors[i] = patternBmp.GetPixel(patternPoints[i].X, patternPoints[i].Y);

                    // Szybkie skanowanie
                    int step = 15;
                    for (int y = 0; y < screenshot.Height - ph; y += step)
                    {
                        for (int x = 0; x < screenshot.Width - pw; x += step)
                        {
                            int matches = 0;
                            
                            for (int i = 0; i < 5; i++)
                            {
                                int sx = x + patternPoints[i].X;
                                int sy = y + patternPoints[i].Y;
                                
                                if (sx < screenshot.Width && sy < screenshot.Height)
                                {
                                    Color sc = screenshot.GetPixel(sx, sy);
                                    if (ColorClose(sc, patternColors[i]))
                                        matches++;
                                }
                            }

                            double score = matches / 5.0;
                            
                            if (score >= _matchThreshold)
                            {
                                return new Rectangle(x, y, pw, ph);
                            }
                        }
                    }
                }
            }
            catch { }

            return Rectangle.Empty;
        }

        private bool ColorClose(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) < 25 && 
                   Math.Abs(a.G - b.G) < 25 && 
                   Math.Abs(a.B - b.B) < 25;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
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