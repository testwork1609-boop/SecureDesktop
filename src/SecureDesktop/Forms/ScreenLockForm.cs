using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SecureDesktop.Utils;

namespace SecureDesktop.Forms
{
    public class ScreenLockForm : Form
    {
        private const uint WDA_MONITOR = 0x00000001;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        private readonly Screen _screen;
        private readonly Dictionary<string, Rectangle> _unlockRegions;
        private readonly Func<string, bool> _verifyPassword;
        private readonly Bitmap _sourceScreenshot;
        private Bitmap _dimmedBackground;
        private Bitmap _lockIconBitmap;

        public Rectangle ScreenBounds => _screen.Bounds;

        public ScreenLockForm(Screen screen, Bitmap sourceScreenshot, Func<string, bool> verifyPassword = null)
        {
            _screen = screen;
            _sourceScreenshot = sourceScreenshot;
            _unlockRegions = new Dictionary<string, Rectangle>();
            _verifyPassword = verifyPassword ?? (pwd => false);
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TrySetDisplayAffinity();
        }

        private void TrySetDisplayAffinity()
        {
            try
            {
                if (!SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE))
                    SetWindowDisplayAffinity(this.Handle, WDA_MONITOR);
            }
            catch { }
        }

        private void InitializeComponent()
        {
            Color primaryColor = Color.FromArgb(45, 165, 90);

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = _screen.Bounds;
            this.Cursor = Cursors.No;
            this.BackColor = Color.Black;
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            if (_sourceScreenshot != null)
            {
                _dimmedBackground = Darken(_sourceScreenshot, 120);
                this.BackgroundImage = _dimmedBackground;
                this.BackgroundImageLayout = ImageLayout.None;
            }

            _lockIconBitmap = CreateLockIconBitmap(56, Color.White);

            var lockIcon = new PictureBox
            {
                Size = new Size(72, 72),
                Location = new Point(this.Width - 96, 20),
                BackColor = primaryColor,
                Cursor = Cursors.Hand,
                Image = _lockIconBitmap,
                SizeMode = PictureBoxSizeMode.CenterImage
            };
            lockIcon.Click += (s, e) => ShowUnlockDialog();

            this.Controls.Add(lockIcon);

            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.L)
                {
                    e.SuppressKeyPress = true;
                    ShowUnlockDialog();
                }
            };
        }

        private static Bitmap Darken(Bitmap source, int alpha)
        {
            var bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImageUnscaled(source, 0, 0);
                using (var brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                    g.FillRectangle(brush, 0, 0, bmp.Width, bmp.Height);
            }
            return bmp;
        }

        private static Bitmap CreateLockIconBitmap(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var brush = new SolidBrush(color))
                    g.FillRectangle(brush, size / 4, size / 2, size / 2, size / 2 - 2);

                using (var pen = new Pen(color, Math.Max(3, size / 12)))
                    g.DrawArc(pen, size / 4 + 2, size / 8, size / 2 - 4, size / 3, 180, 180);

                using (var brush = new SolidBrush(Color.FromArgb(45, 165, 90)))
                {
                    g.FillEllipse(brush, size * 5 / 12, size * 7 / 12, size / 6, size / 8);
                    g.FillRectangle(brush, size * 11 / 24, size * 8 / 12, size / 12, size / 6);
                }
            }
            return bmp;
        }

        public void AddUnlockRegion(string patternKey, Rectangle region)
        {
            _unlockRegions[patternKey] = region;
            this.Invalidate();
        }

        public void RemoveUnlockRegion(string patternKey)
        {
            if (_unlockRegions.Remove(patternKey))
                this.Invalidate();
        }

        public void RemoveAllUnlockRegions()
        {
            if (_unlockRegions.Count > 0)
            {
                _unlockRegions.Clear();
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_unlockRegions.Count == 0) return;

            using (var pen = new Pen(Color.FromArgb(190, 190, 190), 2))
            {
                foreach (var region in _unlockRegions.Values)
                {
                    if (region.Width > 0 && region.Height > 0)
                        e.Graphics.DrawRectangle(pen, region);
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;

            if (m.Msg == WM_NCHITTEST)
            {
                int sx = unchecked((short)(long)m.LParam);
                int sy = unchecked((short)((long)m.LParam >> 16));
                var clientPoint = this.PointToClient(new Point(sx, sy));

                foreach (var region in _unlockRegions.Values)
                {
                    if (region.Contains(clientPoint))
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }
            }

            base.WndProc(ref m);
        }

        private void ShowUnlockDialog()
        {
            Color primaryColor = Color.FromArgb(45, 165, 90);

            using (var dialog = new Form
            {
                Text = "Odblokuj ekran",
                Size = new Size(360, 240),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                BackColor = Color.White,
                KeyPreview = true,
                ShowInTaskbar = false
            })
            {
                var icon = new Label { Text = "🔒", Font = UiFonts.Segoe24, Location = new Point(20, 15), Size = new Size(60, 40) };
                var title = new Label { Text = "Wprowadź hasło, aby odblokować", Font = UiFonts.Segoe11Bold, Location = new Point(75, 22), AutoSize = true };
                var passBox = new TextBox { Location = new Point(30, 80), Size = new Size(290, 30), PasswordChar = '●', Font = UiFonts.Segoe12 };
                var errorLabel = new Label { Location = new Point(30, 118), Size = new Size(290, 20), ForeColor = Color.Red, Visible = false };

                var unlockBtn = new Button { Text = "Odblokuj", Location = new Point(80, 150), Size = new Size(100, 36), BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                unlockBtn.FlatAppearance.BorderSize = 0;

                var cancelBtn = new Button { Text = "Anuluj", Location = new Point(190, 150), Size = new Size(100, 36), BackColor = Color.White, FlatStyle = FlatStyle.Flat };

                Action unlockAction = () =>
                {
                    if (_verifyPassword(passBox.Text))
                    {
                        dialog.Close();
                        this.Close();
                    }
                    else
                    {
                        errorLabel.Text = "Nieprawidłowe hasło!";
                        errorLabel.Visible = true;
                        passBox.Text = "";
                        passBox.Focus();
                    }
                };

                unlockBtn.Click += (s, args) => unlockAction();
                cancelBtn.Click += (s, args) => dialog.Close();

                passBox.KeyDown += (s, args) =>
                {
                    if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; unlockAction(); }
                };

                dialog.KeyDown += (s, args) =>
                {
                    if (args.KeyCode == Keys.Enter) { args.SuppressKeyPress = true; unlockAction(); }
                    else if (args.KeyCode == Keys.Escape) dialog.Close();
                };

                dialog.Controls.AddRange(new Control[] { icon, title, passBox, errorLabel, unlockBtn, cancelBtn });
                dialog.Shown += (s, args) => passBox.Focus();
                dialog.ShowDialog(this);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lockIconBitmap?.Dispose();
                _lockIconBitmap = null;

                _dimmedBackground?.Dispose();
                _dimmedBackground = null;

                _sourceScreenshot?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
