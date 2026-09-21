using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SecureDesktop.Utils;

namespace SecureDesktop.Forms
{
    public class ScreenLockForm : Form
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const int RGN_DIFF = 4;
        private const int FRAME_THICKNESS = 2;
        private static readonly Color PrimaryColor = Color.FromArgb(45, 165, 90);
        private static readonly Color PrimaryHover = Color.FromArgb(60, 190, 110);

        private readonly Screen _screen;
        private readonly Dictionary<string, Rectangle> _unlockRegions;
        private readonly Func<string, bool> _verifyPassword;
        private readonly Bitmap _sourceScreenshot;

        public Rectangle ScreenBounds => _screen.Bounds;

        public ScreenLockForm(Screen screen, Bitmap sourceScreenshot, Func<string, bool> verifyPassword = null)
        {
            _screen = screen;
            _sourceScreenshot = sourceScreenshot;
            _unlockRegions = new Dictionary<string, Rectangle>();
            _verifyPassword = verifyPassword ?? (pwd => false);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = _screen.Bounds;
            this.Cursor = Cursors.No;
            this.BackColor = Color.Black;
            this.Opacity = 0.08;
            this.AllowTransparency = true;
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            // --- Duża, okrągła kłódka w prawym górnym rogu ---
            var unlockContainer = new Panel
            {
                Size = new Size(220, 240),
                Location = new Point(this.Width - 250, 20),
                BackColor = Color.Transparent
            };

            var circle = new Panel
            {
                Size = new Size(140, 140),
                Location = new Point(40, 0),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            circle.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool hovered = circle.Tag as string == "hover";
                Color fill = hovered ? PrimaryHover : PrimaryColor;

                // Zewnętrzny pierścień (biały, półprzezroczysty)
                using (var brush = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                    e.Graphics.FillEllipse(brush, 0, 0, 140, 140);

                // Cienki drugi pierścień (obwódka)
                using (var pen = new Pen(Color.FromArgb(200, 20, 120, 60), 3))
                    e.Graphics.DrawEllipse(pen, 4, 4, 132, 132);

                // Zielone koło
                using (var brush = new SolidBrush(fill))
                    e.Graphics.FillEllipse(brush, 10, 10, 120, 120);

                // Ikona kłódki (biała), wyśrodkowana
                using (var brush = new SolidBrush(Color.White))
                {
                    // Korpus kłódki
                    e.Graphics.FillRectangle(brush, 48, 68, 44, 42);
                    // Pałąk
                    using (var pen = new Pen(Color.White, 8))
                        e.Graphics.DrawArc(pen, 52, 32, 36, 40, 180, 180);
                    // Dziurka
                    using (var greenBrush = new SolidBrush(fill))
                    {
                        e.Graphics.FillEllipse(greenBrush, 63, 82, 14, 12);
                        e.Graphics.FillRectangle(greenBrush, 67, 90, 6, 14);
                    }
                }
            };
            circle.MouseEnter += (s, e) => { circle.Tag = "hover"; circle.Invalidate(); };
            circle.MouseLeave += (s, e) => { circle.Tag = null; circle.Invalidate(); };
            circle.Click += (s, e) => ShowUnlockDialog();

            // Etykieta pod kółkiem
            var hint = new Label
            {
                Text = "Kliknij, aby odblokować   (Ctrl+L)",
                Font = UiFonts.Segoe10Bold,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(180, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 155),
                Size = new Size(220, 34)
            };

            unlockContainer.Controls.Add(circle);
            unlockContainer.Controls.Add(hint);
            this.Controls.Add(unlockContainer);

            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.L)
                {
                    e.SuppressKeyPress = true;
                    ShowUnlockDialog();
                }
            };
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
                var inner = new Rectangle(
                    region.X + FRAME_THICKNESS,
                    region.Y + FRAME_THICKNESS,
                    region.Width - FRAME_THICKNESS * 2,
                    region.Height - FRAME_THICKNESS * 2);

                if (inner.Width <= 0 || inner.Height <= 0) continue;

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

            using (var brush = new SolidBrush(Color.FromArgb(200, 200, 200)))
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

        private void ShowUnlockDialog()
        {
            using (var dialog = new Form
            {
                Text = "Odblokuj ekran",
                Size = new Size(380, 250),
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
                var passBox = new TextBox { Location = new Point(30, 85), Size = new Size(310, 30), PasswordChar = '●', Font = UiFonts.Segoe12 };
                var errorLabel = new Label { Location = new Point(30, 122), Size = new Size(310, 20), ForeColor = Color.Red, Visible = false };

                var unlockBtn = new Button { Text = "Odblokuj", Location = new Point(80, 155), Size = new Size(100, 38), BackColor = PrimaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = UiFonts.Segoe10Bold };
                unlockBtn.FlatAppearance.BorderSize = 0;
                var cancelBtn = new Button { Text = "Anuluj", Location = new Point(200, 155), Size = new Size(100, 38), BackColor = Color.White, FlatStyle = FlatStyle.Flat };

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
                _sourceScreenshot?.Dispose();
            base.Dispose(disposing);
        }
    }
}
