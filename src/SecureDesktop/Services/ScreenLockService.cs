using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

        private static readonly object _logLock = new object();

        public event EventHandler LockActivated;
        public event EventHandler LockDeactivated;

        public bool IsLocked { get { return _isLocked; } }

        public Func<string, bool> PasswordVerifier { get; set; }

        public ScreenLockService()
        {
            _overlays = new List<Form>();
        }

        private static void Log(string msg)
        {
            try
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(dir);
                lock (_logLock)
                {
                    File.AppendAllText(Path.Combine(dir, "lock.log"),
                        "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + msg + "\r\n");
                }
            }
            catch { }
        }

        public void LockAllScreens()
        {
            if (_isLocked)
            {
                Log("LockAllScreens: already locked, skip");
                return;
            }

            _isLocked = true;
            Log("LockAllScreens: start");

            try
            {
                var screenshots = CaptureAllScreens();
                Log("LockAllScreens: captured " + screenshots.Count + " screenshots, AllScreens=" + Screen.AllScreens.Length);

                foreach (var screen in Screen.AllScreens)
                {
                    Bitmap shot;
                    screenshots.TryGetValue(screen, out shot);

                    var overlay = new ScreenLockForm(screen, shot, PasswordVerifier);
                    overlay.UnlockAllRequested += OnUnlockAllRequested;

                    var local = overlay;
                    overlay.FormClosed += (s, e) =>
                    {
                        _overlays.Remove(local);
                        Log("Overlay closed, remaining=" + _overlays.Count);
                        if (_overlays.Count == 0)
                        {
                            _isLocked = false;
                            CleanupPatternService();
                            OnLockDeactivated();
                            Log("All overlays closed, LockDeactivated");
                        }
                    };

                    _overlays.Add(overlay);
                    overlay.Show();
                }

                OnLockActivated();
                Log("LockAllScreens: overlays shown=" + _overlays.Count);
            }
            catch (Exception ex)
            {
                Log("LockAllScreens ERROR: " + ex.Message);
                _isLocked = false;
            }
        }

        public void LockWithPatterns(List<Pattern> patterns, PatternRecognitionService patternService)
        {
            if (_isLocked)
            {
                Log("LockWithPatterns: already locked, skip");
                return;
            }
            if (patterns == null || patterns.Count == 0) return;

            _isLocked = true;
            Log("LockWithPatterns: start, patterns=" + patterns.Count);

            try
            {
                _patternService = patternService ?? new PatternRecognitionService();

                var screenshots = CaptureAllScreens();

                foreach (var screen in Screen.AllScreens)
                {
                    Bitmap shot;
                    screenshots.TryGetValue(screen, out shot);

                    var overlay = new ScreenLockForm(screen, shot, PasswordVerifier);
                    overlay.UnlockAllRequested += OnUnlockAllRequested;

                    var local = overlay;
                    overlay.FormClosed += (s, e) =>
                    {
                        _overlays.Remove(local);
                        Log("Overlay closed, remaining=" + _overlays.Count);
                        if (_overlays.Count == 0)
                        {
                            CleanupPatternService();
                            _isLocked = false;
                            OnLockDeactivated();
                            Log("All overlays closed, LockDeactivated");
                        }
                    };

                    _overlays.Add(overlay);
                    overlay.Show();
                }

                _patternService.PatternFound += OnPatternFound;
                _patternService.PatternLost += OnPatternLost;

                _patternService.Start(patterns);

                OnLockActivated();
                Log("LockWithPatterns: overlays shown=" + _overlays.Count);
            }
            catch (Exception ex)
            {
                Log("LockWithPatterns ERROR: " + ex.Message);
                UnlockScreens();
            }
        }

        private void OnUnlockAllRequested(object sender, EventArgs e)
        {
            Log("OnUnlockAllRequested -> UnlockScreens()");
            UnlockScreens();
        }

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
                catch (Exception ex) { Log("Capture error: " + ex.Message); }
            }
            return result;
        }

        private void OnPatternFound(object sender, PatternFoundEventArgs e)
        {
            if (e.Pattern == null || e.Location == Rectangle.Empty) return;

            Rectangle absoluteRegion = e.Location;
            string key = e.Pattern.Name ?? ("pattern_" + e.Pattern.Id);

            foreach (var overlay in _overlays)
            {
                if (overlay is ScreenLockForm && !overlay.IsDisposed)
                {
                    var lockForm = (ScreenLockForm)overlay;
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
                if (overlay is ScreenLockForm && !overlay.IsDisposed)
                {
                    var lockForm = (ScreenLockForm)overlay;
                    if (lockForm.InvokeRequired)
                        lockForm.BeginInvoke(new Action(() => lockForm.RemoveUnlockRegion(key)));
                    else
                        lockForm.RemoveUnlockRegion(key);
                }
            }
        }

        public void UnlockScreens()
        {
            if (_overlays.Count == 0 && !_isLocked) return;

            Log("UnlockScreens: closing " + _overlays.Count + " overlays");

            try
            {
                CleanupPatternService();

                var toClose = _overlays.ToArray();
                _overlays.Clear();

                foreach (var overlay in toClose)
                {
                    if (overlay != null && !overlay.IsDisposed)
                    {
                        try
                        {
                            if (overlay.InvokeRequired)
                            {
                                var local = overlay;
                                local.BeginInvoke(new Action(() =>
                                {
                                    try { local.Close(); } catch { }
                                }));
                            }
                            else
                            {
                                overlay.Close();
                            }
                        }
                        catch (Exception ex) { Log("close overlay: " + ex.Message); }
                    }
                }

                _isLocked = false;
                OnLockDeactivated();
                Log("UnlockScreens: done");
            }
            catch (Exception ex)
            {
                Log("UnlockScreens ERROR: " + ex.Message);
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

        protected virtual void OnLockActivated() { var h = LockActivated; if (h != null) h(this, EventArgs.Empty); }
        protected virtual void OnLockDeactivated() { var h = LockDeactivated; if (h != null) h(this, EventArgs.Empty); }
    }
}
