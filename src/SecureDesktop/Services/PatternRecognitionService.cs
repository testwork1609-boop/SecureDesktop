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

        /// <summary>
        /// Starts pattern detection loop
        /// </summary>
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
                        // Zrób screenshot
                        using (var screenshot = CaptureScreen())
                        {
                            if (screenshot != null)
                            {
                                foreach (var pattern in _currentPatterns.Where(p => p.IsActive))
                                {
                                    if (_cts.Token.IsCancellationRequested) break;

                                    // Szukaj patternu na screenshocie
                                    var location = FindPattern(screenshot, pattern);
                                    
                                    if (location != Rectangle.Empty)
                                    {
                                        // Pattern znaleziony!
                                        PatternFound?.Invoke(this, new PatternFoundEventArgs
                                        {
                                            Pattern = pattern,
                                            Location = location,
                                            Confidence = 0.95
                                        });
                                    }
                                    else
                                    {
                                        // Pattern nie znaleziony
                                        PatternLost?.Invoke(this, new PatternLostEventArgs
                                        {
                                            Pattern = pattern
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Pattern search error: {ex.Message}");
                    }

                    // Czekaj 500ms przed kolejnym skanowaniem
                    Thread.Sleep(500);
                }
            }, _cts.Token);
        }

        /// <summary>
        /// Stops pattern detection
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            
            _cts?.Cancel();
            _isRunning = false;
        }

        /// <summary>
        /// Captures the entire screen
        /// </summary>
        private Bitmap CaptureScreen()
        {
            try
            {
                var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                var bitmap = new Bitmap(bounds.Width, bounds.Height);
                
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                }
                
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Szuka patternu na screenshocie używając prostego porównania pikseli
        /// </summary>
        private Rectangle FindPattern(Bitmap screenshot, Pattern pattern)
        {
            try
            {
                if (pattern.ImageData == null || pattern.ImageData.Length == 0)
                    return Rectangle.Empty;

                // Wczytaj obraz patternu
                using (var ms = new MemoryStream(pattern.ImageData))
                using (var patternImage = Image.FromStream(ms) as Bitmap)
                {
                    if (patternImage == null) return Rectangle.Empty;

                    // Proste przeszukiwanie - sprawdź czy pattern jest na ekranie
                    for (int y = 0; y < screenshot.Height - patternImage.Height; y += 10)
                    {
                        for (int x = 0; x < screenshot.Width - patternImage.Width; x += 10)
                        {
                            if (ComparePixels(screenshot, patternImage, x, y))
                            {
                                return new Rectangle(x, y, patternImage.Width, patternImage.Height);
                            }
                        }
                    }
                }
            }
            catch { }
            
            return Rectangle.Empty;
        }

        /// <summary>
        /// Porównuje piksele w danym miejscu
        /// </summary>
        private bool ComparePixels(Bitmap source, Bitmap pattern, int startX, int startY)
        {
            int matchCount = 0;
            int totalPixels = 0;
            
            // Porównaj co 5 piksel dla wydajności
            for (int py = 0; py < pattern.Height; py += 5)
            {
                for (int px = 0; px < pattern.Width; px += 5)
                {
                    if (startX + px >= source.Width || startY + py >= source.Height)
                        return false;

                    Color sourcePixel = source.GetPixel(startX + px, startY + py);
                    Color patternPixel = pattern.GetPixel(px, py);
                    
                    totalPixels++;
                    
                    if (Math.Abs(sourcePixel.R - patternPixel.R) < 30 &&
                        Math.Abs(sourcePixel.G - patternPixel.G) < 30 &&
                        Math.Abs(sourcePixel.B - patternPixel.B) < 30)
                    {
                        matchCount++;
                    }
                }
            }

            if (totalPixels == 0) return false;
            
            double matchRate = (double)matchCount / totalPixels;
            return matchRate >= _matchThreshold;
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