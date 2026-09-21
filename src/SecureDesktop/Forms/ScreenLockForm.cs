using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SecureDesktop.Utils;

namespace SecureDesktop.Forms
{
    public class ScreenLockForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        private const int RGN_DIFF = 4;
        private const int FRAME_THICKNESS = 2;

        private readonly Screen _screen;
        private readonly Dictionary<string, Rectangle> _unlockRegions;
        private readonly Func<string, bool> _verifyPassword;
        private readonly Bitmap _sourceScreenshot;
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
            try { SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE); }
            catch { }

            // Po utworzeniu uchwytu od razu ustaw region (jeśli już mamy unlocki).
            UpdateWindowRegion();
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
            this.Opacity = 0.10;              // lekkie przygaszenie pulpitu
            this.AllowTransparency = true;    // wymagane dla Opacity
            this.DoubleBuffered = true;
            this.KeyPreview = true;

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
            UpdateWindowRegion();
            this.Invalidate();
        }

        public void RemoveUnlockRegion(string patternKey)
        {
            if (_unlockRegions.Remove(patternKey))
            {
                UpdateWindowRegion();
                this.Invalidate();
            }
        }

        public void RemoveAllUnlockRegions()
        {
            if (_unlockRegions.Count > 0)
            {
                _unlockRegions.Clear();
                UpdateWindowRegion();
                this.Invalidate();
            }
        }

        /// <summary>
        /// Wycina z okna regiony odblokowane. W tych miejscach forma nie
        /// istnieje fizycznie — kliknięcia przechodzą do pulpitu, a kursor
        /// jest normalny (bez Cursors.No). Zachowujemy 2px pierścień
        /// formy dookoła każdego regionu, żeby narysować szarą ramkę.
        /// </summary>
        private void UpdateWindowRegion()
        {
            if (!this.IsHandleCreated) return;

            if (_unlockRegions.Count == 0)
            {
                SetWindowRgn(this.Handle, IntPtr.Zero, true);
                return;
            }

            IntPtr total = CreateRectRgn(0, 0, this.Width, this.Height);
            if (total == IntPtr.Zero) return;

            foreach (var region in _unlockRegions.Values)
            {
                if (region.Width <= FRAME_THICKNESS * 2 || region.Height <= FRAME_THICKNESS * 2)
                    continue;

                var inner = new Rectangle(
                    region.X + FRAME_THICKNESS,
                    region.Y + FRAME_THICKNESS,
                    region.Width - FRAME_THICKNESS * 2,
                    region.Height - FRAME_THICKNESS * 2);

                IntPtr hole = CreateRectRgn(inner.Left, inner.Top, inner.Right, inner.Bottom);
                if (hole == IntPtr.Zero) continue;

                CombineRgn(total, total, hole, RGN_DIFF);
                DeleteObject(hole);
            }

            SetWindowRgn(this.Handle, total, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_unlockRegions.Count == 0) return;

            using (var brush = new SolidBrush(Color.FromArgb(190, 190, 190)))
            {
                foreach (var region in _unlockRegions.Values)
                {
                    if (region.Width <= FRAME_THICKNESS * 2 || region.Height <= FRAME_THICKNESS * 2)
                        continue;

                    int t = FRAME_THICKNESS;
                    e.Graphics.FillRectangle(brush, region.X, region.Y, region.Width, t);
                    e.Graphics.FillRectangle(brush, region.X, region.Bottom - t, region.Width, t);
                    e.Graphics.FillRectangle(brush, region.X, region.Y, t, region.Height);
                    e.Graphics.FillRectangle(brush, region.Right - t, region.Y, t, region.Height);
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
                _sourceScreenshot?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
