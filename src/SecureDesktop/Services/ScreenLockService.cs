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
        private bool _unlockInProgress;
        private DateTime _unlockCooldownUntil = DateTime.MinValue;
        private PatternRecognitionService _patternService;

        private static readonly object _logLock = new object();

        public event EventHandler LockActivated;
        public event EventHandler LockDeactivated;

        /// <summary>
        /// Wywoływane po odblokowaniu ekranu, z użytkownikiem, który podał
        /// poprawne dane logowania. DashboardForm używa tego do przełączenia
        /// sesji, jeśli odblokował inny użytkownik.
        /// </summary>
        public event Action<User> UnlockedByUser;

        public bool IsLocked { get { return _isLocked; } }

        /// <summary>
        /// Callback weryfikujący dane logowania: (pin, hasło) → User lub null.
        /// </summary>
        public Func<string, string, User> CredentialsVerifier { get; set; }

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

        private bool IsInCooldown()
        {
            return DateTime.UtcNow < _unlockCooldownUntil;
        }

        public void LockAllScreens()
        {
            if (IsInCooldown())
            {
                Log("LockAllScreens: in cooldown, skip");
                return;
            }
            if (_isLocked || _unlockInProgress)
            {
                Log("LockAllScreens: already locked or unlocking, skip");
                return;
            }

            _isLocked = true;
            Log("LockAllScreens: start");

            try
            {
                var screenshots = CaptureAllScreens();

                foreach (var screen in Screen.AllScreens)
                {
                    Bitmap shot;
                    screenshots.TryGetValue(screen, out shot);

                    var overlay = new ScreenLockForm(screen, shot, CredentialsVerifier);
                    overlay.UnlockAllRequested += OnUnlockAllRequested;

                    var local = overlay;
                    overlay.FormClosed += (s, e) =>
                    {
                        _overlays.Remove(local);
                        Log("Overlay closed, remaining=" + _overlays.Count);
                        if (_overlays.Count == 0 && _isLocked)
                        {
                            _isLocked = false;
                            CleanupPatternService();
                            OnLockDeactivated();
                            Log("LockDeactivated (all overlays closed)");
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
            if (IsInCooldown())
            {
                Log("LockWithPatterns: in cooldown, skip");
                return;
            }
            if (_isLocked || _unlockInProgress)
            {
                Log("LockWithPatterns: already locked or unlocking, skip");
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

                    var overlay = new ScreenLockForm(screen, shot, CredentialsVerifier);
                    overlay.UnlockAllRequested += OnUnlockAllRequested;

                    var local = overlay;
                    overlay.FormClosed += (s, e) =>
                    {
                        _overlays.Remove(local);
                        Log("Overlay closed, remaining=" + _overlays.Count);
                        if (_overlays.Count == 0 && _isLocked)
                        {
                            CleanupPatternService();
                            _isLocked = false;
                            OnLockDeactivated();
                            Log("LockDeactivated (all overlays closed)");
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

        private void OnUnlockAllRequested(object sender, UnlockAllEventArgs e)
        {
            Log("OnUnlockAllRequested for user " +
                (e != null && e.User != null ? e.User.IdentificationNumber : "null"));
            UnlockScreens(e != null ? e.User : null);
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

            foreach (var overlay in _overlays.ToArray())
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
                        lockForm.BeginInvoke(new Action(() => { try { lockForm.AddUnlockRegion(key, localRegion); } catch { } }));
                    else
                        lockForm.AddUnlockRegion(key, localRegion);
                }
            }
        }

        private void OnPatternLost(object sender, PatternLostEventArgs e)
        {
            string key = e.Pattern.Name ?? ("pattern_" + e.Pattern.Id);

            foreach (var overlay in _overlays.ToArray())
            {
                if (overlay is ScreenLockForm && !overlay.IsDisposed)
                {
                    var lockForm = (ScreenLockForm)overlay;
                    if (lockForm.InvokeRequired)
                        lockForm.BeginInvoke(new Action(() => { try { lockForm.RemoveUnlockRegion(key); } catch { } }));
                    else
                        lockForm.RemoveUnlockRegion(key);
                }
            }
        }

        public void UnlockScreens(User unlockedBy = null)
        {
            if (_unlockInProgress)
            {
                Log("UnlockScreens: already in progress, skip");
                return;
            }
            if (_overlays.Count == 0 && !_isLocked)
            {
                Log("UnlockScreens: nothing to unlock");
                return;
            }

            _unlockInProgress = true;
            _unlockCooldownUntil = DateTime.UtcNow.AddSeconds(1);
            Log("UnlockScreens START, overlays=" + _overlays.Count);

            try
            {
                CleanupPatternService();

                var toClose = new List<Form>(_overlays);

                foreach (var overlay in toClose)
                {
                    try
                    {
                        if (overlay != null && !overlay.IsDisposed && overlay.IsHandleCreated)
                        {
                            var local = overlay;
                            local.BeginInvoke(new Action(() =>
                            {
                                try { local.Close(); }
                                catch (Exception ex) { Log("close inner: " + ex.Message); }
                            }));
                        }
                    }
                    catch (Exception ex) { Log("close outer: " + ex.Message); }
                }

                if (toClose.Count == 0 && _isLocked)
                {
                    _isLocked = false;
                    OnLockDeactivated();
                }

                // Powiadom o użytkowniku, który odblokował (jeśli był).
                if (unlockedBy != null)
                {
                    var h = UnlockedByUser;
                    if (h != null)
                    {
                        try { h(unlockedBy); } catch (Exception ex) { Log("UnlockedByUser: " + ex.Message); }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("UnlockScreens ERROR: " + ex.Message);
            }
            finally
            {
                _unlockInProgress = false;
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

    /// <summary>
    /// Argumenty zdarzenia odblokowania — zawierają użytkownika,
    /// który podał poprawne dane logowania.
    /// </summary>
    public class UnlockAllEventArgs : EventArgs
    {
        public User User { get; set; }
    }
}
