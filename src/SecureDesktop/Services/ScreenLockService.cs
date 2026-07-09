using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SecureDesktop.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Services
{
    public class ScreenLockService
    {
        private List<Form> _overlays = new List<Form>();
        private bool _isLocked;

        public event EventHandler LockActivated;
        public event EventHandler LockDeactivated;

        public ScreenLockService()
        {
        }

        public void LockAllScreens()
        {
            if (_isLocked) return;

            foreach (var screen in Screen.AllScreens)
            {
                var overlay = new ScreenLockForm(screen);
                _overlays.Add(overlay);
                overlay.Show();
            }

            _isLocked = true;
            LockActivated?.Invoke(this, EventArgs.Empty);
        }

        public void UnlockScreens()
        {
            if (!_isLocked) return;

            foreach (var overlay in _overlays)
            {
                if (overlay != null && !overlay.IsDisposed)
                {
                    overlay.BeginInvoke(new Action(() =>
                    {
                        overlay.Close();
                        overlay.Dispose();
                    }));
                }
            }
            
            _overlays.Clear();
            _isLocked = false;
            LockDeactivated?.Invoke(this, EventArgs.Empty);
        }
    }
}