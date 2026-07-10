using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SecureDesktop.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Services
{
    /// <summary>
    /// Service responsible for managing screen lock functionality
    /// Supports full screen lock and pattern-based lock
    /// </summary>
    public class ScreenLockService
    {
        private List<Form> _overlays;
        private bool _isLocked;
        private PatternRecognitionService _patternService;
        private readonly object _lockObject = new object();

        public event EventHandler LockActivated;
        public event EventHandler LockDeactivated;

        public bool IsLocked => _isLocked;

        public ScreenLockService()
        {
            _overlays = new List<Form>();
        }

        /// <summary>
        /// Locks all screens completely with a semi-transparent overlay
        /// </summary>
        public void LockAllScreens()
        {
            if (_isLocked)
            {
                return;
            }

            try
            {
                var screens = Screen.AllScreens;

                foreach (var screen in screens)
                {
                    var overlay = new ScreenLockForm(screen);
                    overlay.FormClosed += (s, e) =>
                    {
                        // Gdy wszystkie nakładki zamknięte, odblokuj
                        _overlays.Remove(overlay);
                        if (_overlays.Count == 0)
                        {
                            _isLocked = false;
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
                throw;
            }
        }

        /// <summary>
        /// Locks screens with pattern recognition enabled
        /// Detected patterns create transparent "holes" in the overlay
        /// </summary>
        public void LockWithPatterns(List<Pattern> patterns, PatternRecognitionService patternService)
        {
            if (_isLocked)
            {
                return;
            }

            if (patterns == null || patterns.Count == 0)
            {
                throw new ArgumentException("At least one pattern is required for pattern lock.");
            }

            try
            {
                _patternService = patternService ?? new PatternRecognitionService();

                // Create overlays for all screens
                var screens = Screen.AllScreens;

                foreach (var screen in screens)
                {
                    var overlay = new ScreenLockForm(screen);
                    overlay.FormClosed += (s, e) =>
                    {
                        _overlays.Remove(overlay);
                        if (_overlays.Count == 0)
                        {
                            StopPatternRecognition();
                            _isLocked = false;
                            OnLockDeactivated();
                        }
                    };

                    _overlays.Add(overlay);
                    overlay.Show();
                }

                // Subscribe to pattern detection events
                _patternService.PatternFound += OnPatternFound;
                _patternService.PatternLost += OnPatternLost;

                // Start pattern detection
                _patternService.Start(patterns);

                _isLocked = true;
                OnLockActivated();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error locking screens with patterns: {ex.Message}");
                UnlockScreens();
                throw;
            }
        }

        /// <summary>
        /// Unlocks all screens and stops pattern recognition
        /// </summary>
        public void UnlockScreens()
        {
            if (!_isLocked)
            {
                return;
            }

            try
            {
                // Stop pattern recognition first
                StopPatternRecognition();

                // Close all overlay forms
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
                        catch
                        {
                            // Form might already be disposed
                        }
                    }
                }

                _overlays.Clear();
                _isLocked = false;
                OnLockDeactivated();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unlocking screens: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles pattern found event from PatternRecognitionService
        /// Creates transparent regions in the overlay
        /// </summary>
        private void OnPatternFound(object sender, PatternFoundEventArgs e)
        {
            if (e.Pattern == null || e.Location == Rectangle.Empty)
            {
                return;
            }

            // Calculate region with safety margins
            var region = new Rectangle(
                e.Location.X - e.Pattern.MarginLeft,
                e.Location.Y - e.Pattern.MarginTop,
                e.Location.Width + e.Pattern.MarginLeft + e.Pattern.MarginRight,
                e.Location.Height + e.Pattern.MarginTop + e.Pattern.MarginBottom
            );

            // Ensure region is not negative
            if (region.X < 0) region.X = 0;
            if (region.Y < 0) region.Y = 0;

            // Add unlock region to all overlays
            foreach (var overlay in _overlays)
            {
                if (overlay is ScreenLockForm lockForm && !lockForm.IsDisposed)
                {
                    if (lockForm.InvokeRequired)
                    {
                        lockForm.BeginInvoke(new Action(() =>
                        {
                            lockForm.AddUnlockRegion(region);
                        }));
                    }
                    else
                    {
                        lockForm.AddUnlockRegion(region);
                    }
                }
            }
        }

        /// <summary>
        /// Handles pattern lost event from PatternRecognitionService
        /// Removes transparent regions from the overlay
        /// </summary>
        private void OnPatternLost(object sender, PatternLostEventArgs e)
        {
            // Remove unlock regions from all overlays
            foreach (var overlay in _overlays)
            {
                if (overlay is ScreenLockForm lockForm && !lockForm.IsDisposed)
                {
                    if (lockForm.InvokeRequired)
                    {
                        lockForm.BeginInvoke(new Action(() =>
                        {
                            lockForm.RemoveUnlockRegions();
                        }));
                    }
                    else
                    {
                        lockForm.RemoveUnlockRegions();
                    }
                }
            }
        }

        /// <summary>
        /// Stops pattern recognition service safely
        /// </summary>
        private void StopPatternRecognition()
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping pattern recognition: {ex.Message}");
                }
                finally
                {
                    _patternService = null;
                }
            }
        }

        /// <summary>
        /// Gets the number of active overlays
        /// </summary>
        public int OverlayCount => _overlays?.Count ?? 0;

        /// <summary>
        /// Checks if pattern recognition is active
        /// </summary>
        public bool IsPatternRecognitionActive => _patternService != null;

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