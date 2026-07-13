using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        public PatternRecognitionService(double matchThreshold = 0.55)
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
                                    
                                    var location = FindPatternOnScreen(screenshot, pattern);
                                    
                                    if (location != Rectangle.Empty)
                                    {
                                        // Sprawdź czy lokalizacja się zmieniła
                                        if (!_lastFoundLocations.ContainsKey(pattern.Id) ||
                                            _lastFoundLocations[pattern.Id] != location)
                                        {
                                            _lastFoundLocations[pattern.Id] = location;
                                            
                                            PatternFound?.Invoke(this, new PatternFoundEventArgs
                                            {
                                                Pattern = pattern,
                                                Location = location,
                                                Confidence = 0.95
                                            });
                                        }
                                    }
                                    else
                                    {
                                        // Pattern zgubiony
                                        if (_lastFoundLocations.ContainsKey(pattern.Id))
                                        {
                                            _lastFoundLocations.Remove(pattern.Id);
                                            
                                            PatternLost?.Invoke(this, new PatternLostEventArgs
                                            {
                                                Pattern = pattern
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    
                    Thread.Sleep(500);
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

        private Rectangle FindPatternOnScreen(Bitmap screenshot, Pattern pattern)
        {
            try
            {
                if (pattern.ImageData == null || pattern.ImageData.Length == 0)
                    return Rectangle.Empty;

                using (var ms = new MemoryStream(pattern.ImageData))
                using (var originalPattern = new Bitmap(ms))
                {
                    double bestMatch = 0;
                    int bestX = 0, bestY = 0, bestW = 0, bestH = 0;

                    // Skaluj pattern
                    double[] scales = { 0.8, 0.9, 1.0, 1.1, 1.2 };
                    
                    foreach (double scale in scales)
                    {
                        int newW = (int)(originalPattern.Width * scale);
                        int newH = (int)(originalPattern.Height * scale);
                        
                        if (newW < 5 || newH < 5) continue;
                        if (newW > screenshot.Width || newH > screenshot.Height) continue;

                        using (var scaledPattern = new Bitmap(newW, newH))
                        {
                            using (var g = Graphics.FromImage(scaledPattern))
                            {
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.DrawImage(originalPattern, 0, 0, newW, newH);
                            }

                            int step = Math.Max(5, newW / 10);
                            
                            for (int y = 0; y < screenshot.Height - newH; y += step)
                            {
                                for (int x = 0; x < screenshot.Width - newW; x += step)
                                {
                                    double similarity = ComparePixels(screenshot, scaledPattern, x, y);
                                    if (similarity > bestMatch)
                                    {
                                        bestMatch = similarity;
                                        bestX = x;
                                        bestY = y;
                                        bestW = newW;
                                        bestH = newH;
                                    }
                                }
                            }
                        }
                    }

                    if (bestMatch >= _matchThreshold)
                    {
                        return new Rectangle(bestX, bestY, bestW, bestH);
                    }
                }
            }
            catch { }

            return Rectangle.Empty;
        }

        private double ComparePixels(Bitmap source, Bitmap pattern, int startX, int startY)
        {
            int matchCount = 0;
            int totalChecks = 0;

            int step = Math.Max(3, Math.Min(pattern.Width, pattern.Height) / 12);

            for (int py = 0; py < pattern.Height; py += step)
            {
                for (int px = 0; px < pattern.Width; px += step)
                {
                    if (startX + px >= source.Width || startY + py >= source.Height)
                        return 0;

                    Color sp = source.GetPixel(startX + px, startY + py);
                    Color pp = pattern.GetPixel(px, py);
                    totalChecks++;

                    if (Math.Abs(sp.R - pp.R) < 40 && 
                        Math.Abs(sp.G - pp.G) < 40 && 
                        Math.Abs(sp.B - pp.B) < 40)
                    {
                        matchCount++;
                    }
                }
            }

            if (totalChecks == 0) return 0;
            return (double)matchCount / totalChecks;
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