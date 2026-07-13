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
                // TEST: Po 1 sekundzie zgłoś znalezienie KAŻDEGO patternu
                Thread.Sleep(1000);

                if (!_cts.Token.IsCancellationRequested && _currentPatterns != null)
                {
                    int screenW = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
                    int screenH = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

                    foreach (var pattern in _currentPatterns.Where(p => p.IsActive))
                    {
                        if (_cts.Token.IsCancellationRequested) break;

                        // TEST: Umieść "dziurę" na środku ekranu
                        var testLocation = new Rectangle(
                            screenW / 2 - 200,
                            screenH / 2 - 150,
                            400,
                            300
                        );

                        System.Diagnostics.Debug.WriteLine(
                            $"TEST: Pattern '{pattern.Name}' -> dziura w {testLocation}");

                        PatternFound?.Invoke(this, new PatternFoundEventArgs
                        {
                            Pattern = pattern,
                            Location = testLocation,
                            Confidence = 1.0
                        });
                    }
                }

                // Potem normalne skanowanie (na razie puste)
                while (!_cts.Token.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            _isRunning = false;
            _currentPatterns = null;
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