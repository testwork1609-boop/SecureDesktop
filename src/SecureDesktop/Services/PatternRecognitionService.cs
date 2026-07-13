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

        public event EventHandler<PatternFoundEventArgs> PatternFound;
        public event EventHandler<PatternLostEventArgs> PatternLost;

        public PatternRecognitionService(double matchThreshold = 0.90)
        {
            _matchThreshold = matchThreshold;
        }

        public void Start(List<Pattern> patterns)
        {
            if (_isRunning) return;
            
            _currentPatterns = patterns;
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
                                        PatternFound?.Invoke(this, new PatternFoundEventArgs
                                        {
                                            Pattern = pattern,
                                            Location = location,
                                            Confidence = 0.95
                                        });
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
            _cts?.Cancel();
            _isRunning = false;
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
                using (var patternBmp = new Bitmap(ms))
                {
                    if (patternBmp.Width > screenshot.Width || patternBmp.Height > screenshot.Height)
                        return Rectangle.Empty;

                    for (int y = 0; y < screenshot.Height - patternBmp.Height; y += 20)
                    {
                        for (int x = 0; x < screenshot.Width - patternBmp.Width; x += 20)
                        {
                            double similarity = CompareRegions(screenshot, patternBmp, x, y);
                            if (similarity >= _matchThreshold)
                            {
                                return new Rectangle(x, y, patternBmp.Width, patternBmp.Height);
                            }
                        }
                    }
                }
            }
            catch { }
            
            return Rectangle.Empty;
        }

        private double CompareRegions(Bitmap source, Bitmap pattern, int startX, int startY)
        {
            int matchCount = 0;
            int totalChecks = 0;
            
            for (int py = 0; py < pattern.Height; py += 10)
            {
                for (int px = 0; px < pattern.Width; px += 10)
                {
                    if (startX + px >= source.Width || startY + py >= source.Height)
                        return 0;

                    Color sp = source.GetPixel(startX + px, startY + py);
                    Color pp = pattern.GetPixel(px, py);
                    
                    totalChecks++;
                    
                    if (Math.Abs(sp.R - pp.R) < 40 && Math.Abs(sp.G - pp.G) < 40 && Math.Abs(sp.B - pp.B) < 40)
                        matchCount++;
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