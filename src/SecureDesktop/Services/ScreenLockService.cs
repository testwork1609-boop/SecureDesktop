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

        public ScreenLockService()
        {
            _overlays = new List<Form>();
        }

        public void LockAllScreens()
        {
            if (_isLocked) return;

            try
            {
                var screens = Screen.AllScreens;

                foreach (var screen in screens)
                {
                    var overlay = new ScreenLockForm(screen);
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

                var screens = Screen.AllScreens;

                foreach (var screen in screens)
                {
                    var overlay = new ScreenLockForm(screen);
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

        // Klucz regionu MUSI byc oparty o Id patternu, a nie o Name. Nazwa moze byc
        // pusta lub przypadkowo powielona przez uzytkownika w Konfiguracji - w takim
        // wypadku dwa rozne wzorce nadpisywalyby ten sam wpis w slowniku regionow
        // odblokowania w kazdym ScreenLockForm, przez co tylko jeden z nich realnie
        // odblokowywal ekran, a drugi wygladal na "niedzialajacy".
        private static string KeyFor(Pattern pattern) => "pattern_" + pattern.Id;

        private void OnPatternFound(object sender, PatternFoundEventArgs e)
        {
            if (e.Pattern == null || e.Location == Rectangle.Empty) return;

            var region = new Rectangle(
                e.Location.X - e.Pattern.MarginLeft,
                e.Location.Y - e.Pattern.MarginTop,
                e.Location.Width + e.Pattern.MarginLeft + e.Pattern.MarginRight,
                e.Location.Height + e.Pattern.MarginTop + e.Pattern.MarginBottom
            );

            if (region.X < 0) region.X = 0;
            if (region.Y < 0) region.Y = 0;
            if (region.Width <= 0) region.Width = e.Location.Width;
            if (region.Height <= 0) region.Height = e.Location.Height;

            string key = KeyFor(e.Pattern);

            foreach (var overlay in _overlays)
            {
                if (overlay is ScreenLockForm lockForm && !lockForm.IsDisposed)
                {
                    if (lockForm.InvokeRequired)
                    {
                        lockForm.BeginInvoke(new Action(() => lockForm.AddUnlockRegion(key, region)));
                    }
                    else
                    {
                        lockForm.AddUnlockRegion(key, region);
                    }
                }
            }
        }

        private void OnPatternLost(object sender, PatternLostEventArgs e)
        {
            string key = KeyFor(e.Pattern);

            foreach (var overlay in _overlays)
            {
                if (overlay is ScreenLockForm lockForm && !lockForm.IsDisposed)
                {
                    if (lockForm.InvokeRequired)
                    {
                        lockForm.BeginInvoke(new Action(() => lockForm.RemoveUnlockRegion(key)));
                    }
                    else
                    {
                        lockForm.RemoveUnlockRegion(key);
                    }
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
                            {
                                overlay.BeginInvoke(new Action(() =>
                                {
                                    overlay.Close();
                                    overlay.Dispose();
                                }));
                            }
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
                finally
                {
                    _patternService = null;
                }
            }
        }

        protected virtual void OnLockActivated()
        {
            LockActivated?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnLockDeactivated()
        {
            LockDeactivated?.Invoke(this, EventArgs.Empty);
        }
    }
}