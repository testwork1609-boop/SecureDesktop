using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SecureDesktop.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Services
{
    public class ScreenLockService
    {
        private List<Form> _overlays;
        private bool _isLocked;
        private PatternRecognitionService _patternService;

        public event EventHandler LockActivated;
        public event EventHandler LockDeactivated;

        public bool IsLocked => _isLocked;

        public Func<string, bool> PasswordVerifier { get; set; }

        public ScreenLockService()
        {
            _overlays = new List<Form>();
        }

        public void LockAllScreens()
        {
            if (_isLocked) return;

            try
            {
                var screenshots = CaptureAllScreens();

                foreach (var screen in Screen.AllScreens)
                {
                    Bitmap shot;
                    screenshots.TryGetValue(screen, out shot);

                    var overlay = new ScreenLockForm(screen, shot, PasswordVerifier);
                    overlay.FormClosed += (s, e) =>
                    {
                        _overlays.Remove(overlay);
                        if (_overlays.Count == 0)
                        {
                            _isLocked = false;
                            CleanupPatternService();
                            OnLockDeactivated();
                        }
                    };

                    _overlays.Add(overlay);
                    overlay.Show();
                }

                _isLocked = true;
                OnLockActivated();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error locking screens: {ex.Message}");
            }
        }

        public void LockWithPatterns(List<Pattern> patterns, PatternRecognitionService patternService)
        {
            if (_isLocked) return;
            if (patterns == null || patterns.Count == 0) return;

            try
            {
                _patternService = patternService ?? new PatternRecognitionService();

                var screenshots = CaptureAllScreens();

                foreach (var screen in Screen.AllScreens)
                {
                    Bitmap shot;
                    screenshots.TryGetValue(screen, out shot);

                    var overlay = new ScreenLockForm(screen, shot, PasswordVerifier);
                    overlay.FormClosed += (s, e) =>
                    {
                        _overlays.Remove(overlay);
                        if (_overlays.Count == 0)
                        {
                            CleanupPatternService();
                            _isLocked = false;
                            OnLockDeactivated();
                        }
                    };

                    _overlays.Add(overlay);
                    overlay.Show();
                }

                _patternService.PatternFound += OnPatternFound;
                _patternService.PatternLost += OnPatternLost;

                _patternService.Start(patterns);

                _isLocked = true;
                OnLockActivated();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in pattern lock: {ex.Message}");
                UnlockScreens();
            }
        }

        /// <summary>
        /// Wykonuje zrzut każdego monitora PRZED pokazaniem jakiegokolwiek
        /// overlaya. Każdy ScreenLockForm użyje go jako tła, przyciemnionego
        /// lokalnie - dzięki temu nie potrzebujemy WS_EX_LAYERED, a
        /// SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE) działa poprawnie,
        /// więc PatternRecognitionService widzi żywy (nieprzygaszony) ekran.
        /// </summary>
        private Dictionary<Screen, Bitmap> CaptureAllScreens()
        {
            var result = new Dictionary<Screen, Bitmap>();
            foreach (var screen in Screen.AllScreens)
            {
                try
                {
                    var bounds = screen.Bounds;
                    var bmp = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    result[screen] = bmp;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Capture error: " + ex.Message);
                }
            }
            return result;
        }

        private void OnPatternFound(object sender, PatternFoundEventArgs e)
        {
            if (e.Pattern == null || e.Location == Rectangle.Empty) return;

            // Brak marginesów - ramka ma być dokładnie w miejscu, gdzie
            // znaleziono wzorzec, nie powiększona.
            Rectangle absoluteRegion = e.Location;

            string key = e.Pattern.Name ?? ("pattern_" + e.Pattern.Id);

            foreach (var overlay in _overlays)
            {
                if (overlay is ScreenLockForm lockForm && !lockForm.IsDisposed)
                {
                    var screenBounds = lockForm.ScreenBounds;

                    if (!screenBounds.IntersectsWith(absoluteRegion))
                        continue;

                    var localRegion = new Rectangle(
                        absoluteRegion.X - screenBounds.X,
                        absoluteRegion.Y - screenBounds.Y,
                        absoluteRegion.Width,
                        absoluteRegion.Height);

                    if (lockForm.InvokeRequired)
                        lockForm.BeginInvoke(new Action(() => lockForm.AddUnlockRegion(key, localRegion)));
                    else
                        lockForm.AddUnlockRegion(key, localRegion);
                }
            }
        }

        private void OnPatternLost(object sender, PatternLostEventArgs e)
        {
            string key = e.Pattern.Name ?? ("pattern_" + e.Pattern.Id);

            foreach (var overlay in _overlays)
            {
                if (overlay is ScreenLockForm lockForm && !lockForm.IsDisposed)
                {
                    if (lockForm.InvokeRequired)
                        lockForm.BeginInvoke(new Action(() => lockForm.RemoveUnlockRegion(key)));
                    else
                        lockForm.RemoveUnlockRegion(key);
                }
            }
        }

        public void UnlockScreens()
        {
            if (!_isLocked) return;

            try
            {
                CleanupPatternService();

                foreach (var overlay in _overlays.ToArray())
                {
                    if (overlay != null && !overlay.IsDisposed)
                    {
                        try
                        {
                            if (overlay.InvokeRequired)
                                overlay.BeginInvoke(new Action(() => { overlay.Close(); overlay.Dispose(); }));
                            else
                            {
                                overlay.Close();
                                overlay.Dispose();
                            }
                        }
                        catch { }
                    }
                }

                _overlays.Clear();
                _isLocked = false;
                OnLockDeactivated();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unlocking: {ex.Message}");
            }
        }

        private void CleanupPatternService()
        {
            if (_patternService != null)
            {
                try
                {
                    _patternService.PatternFound -= OnPatternFound;
                    _patternService.PatternLost -= OnPatternLost;
                    _patternService.Stop();
                    _patternService.Dispose();
                }
                catch { }
                finally { _patternService = null; }
            }
        }

        protected virtual void OnLockActivated() => LockActivated?.Invoke(this, EventArgs.Empty);
        protected virtual void OnLockDeactivated() => LockDeactivated?.Invoke(this, EventArgs.Empty);
    }
}
