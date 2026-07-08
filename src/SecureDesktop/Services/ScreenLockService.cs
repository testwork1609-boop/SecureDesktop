using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SecureDesktop.Forms;
using SecureDesktop.Models;
using Serilog;

namespace SecureDesktop.Services
{
    public class ScreenLockService
    {
        private List<Form> _overlays = new List<Form>();
        private bool _isLocked;

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
            Log.Information("All screens locked");
        }

        public void UnlockScreens()
        {
            foreach (var overlay in _overlays)
            {
                if (!overlay.IsDisposed)
                {
                    overlay.Invoke(new Action(() => overlay.Close()));
                }
            }
            _overlays.Clear();
            _isLocked = false;
        }
    }
}