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

        private readonly Screen _screen;
        private readonly Dictionary<string, Rectangle> _unlockRegions;
        private readonly Func<string, bool> _verifyPassword;
        private readonly Bitmap _sourceScreenshot;
        private LockButtonForm _lockButton;

        public Rectangle ScreenBounds { get { return _screen.Bounds; } }

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

            _lockButton = new LockButtonForm(_screen);
            _lockButton.LockClicked += (s, e) => ShowUnlockDialog();

            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.L)
                {
                    e.SuppressKeyPress = true;
                    ShowUnlockDialog();
                }
            };

            this.Shown += (s, e) =>
            {
                if (_lockButton != null && !_lockButton.IsDisposed && !_lockButton.Visible)
                    _lockButton.Show();
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
            if (_lockButton != null && !_lockButton.IsDisposed)
                _lockButton.Hide();

            try
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

                    var unlockBtn = new Button { Text = "Odblokuj", Location = new Point(80, 155), Size = new Size(100, 38), BackColor = Color.FromArgb(45, 165, 90), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = UiFonts.Segoe10Bold };
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
            finally
            {
                if (_lockButton != null && !_lockButton.IsDisposed && this.Visible)
                    _lockButton.Show();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_lockButton != null && !_lockButton.IsDisposed)
                {
                    try { _lockButton.Close(); _lockButton.Dispose(); } catch { }
                    _lockButton = null;
                }
                if (_sourceScreenshot != null) _sourceScreenshot.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Osobne, małe okienko z kłódką. NIE ma ustawionej Opacity (domyślnie 100%)
    /// - użytkownik widzi ikonę w pełni kryjącą. Tło okna jest magenta,
    /// ustawione jako TransparencyKey - widoczne są tylko narysowane elementy
    /// (biały ring + zielone koło + etykieta).
    /// </summary>
    internal class LockButtonForm : Form
    {
        public event EventHandler LockClicked;

        private const int CircleSize = 72;   // mniejsze (było 96)
        private const int OuterPadding = 10;
        private const int LabelHeight = 26;

        public LockButtonForm(Screen screen)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;

            int totalWidth = CircleSize + OuterPadding * 2;
            int totalHeight = OuterPadding + CircleSize + 6 + LabelHeight + OuterPadding;
            this.Size = new Size(totalWidth, totalHeight);
            this.Location = new Point(screen.Bounds.Right - totalWidth - 16, screen.Bounds.Top + 16);

            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Hand;

            this.Paint += OnPaintInternal;
            this.MouseClick += OnMouseClickInternal;
        }

        private void OnPaintInternal(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            int cx = this.Width / 2;
            int cy = OuterPadding + CircleSize / 2;
            int r = CircleSize / 2;

            // Biały pierścień zewnętrzny (dla maksymalnego kontrastu).
            using (var brush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(brush, cx - r, cy - r, CircleSize, CircleSize);

            // Delikatna ciemniejsza obwódka.
            using (var pen = new Pen(Color.FromArgb(30, 120, 60), 2))
                e.Graphics.DrawEllipse(pen, cx - r + 2, cy - r + 2, CircleSize - 4, CircleSize - 4);

            // Zielone koło.
            using (var brush = new SolidBrush(Color.FromArgb(45, 165, 90)))
                e.Graphics.FillEllipse(brush, cx - r + 5, cy - r + 5, CircleSize - 10, CircleSize - 10);

            // Ikona kłódki — skala dopasowana do 72 px koła.
            using (var brush = new SolidBrush(Color.White))
            {
                // Korpus
                e.Graphics.FillRectangle(brush, cx - 11, cy - 1, 22, 18);
                // Pałąk
                using (var pen = new Pen(Color.White, 4))
                    e.Graphics.DrawArc(pen, cx - 8, cy - 15, 16, 18, 180, 180);
                // Dziurka
                using (var greenBrush = new SolidBrush(Color.FromArgb(45, 165, 90)))
                {
                    e.Graphics.FillEllipse(greenBrush, cx - 2, cy + 4, 4, 4);
                    e.Graphics.FillRectangle(greenBrush, cx - 1, cy + 7, 2, 6);
                }
            }

            // Etykieta pod kołem.
            string text = "Odblokuj (Ctrl+L)";
            using (var font = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                var size = e.Graphics.MeasureString(text, font);
                float tx = cx - size.Width / 2f;
                float ty = cy + r + 4;

                using (var bgBrush = new SolidBrush(Color.FromArgb(235, 0, 0, 0)))
                    e.Graphics.FillRectangle(bgBrush, tx - 6, ty - 1, size.Width + 12, size.Height + 2);

                using (var textBrush = new SolidBrush(Color.White))
                    e.Graphics.DrawString(text, font, textBrush, tx, ty);
            }
        }

        private void OnMouseClickInternal(object sender, MouseEventArgs e)
        {
            int cx = this.Width / 2;
            int cy = OuterPadding + CircleSize / 2;
            int r = CircleSize / 2 + 6;
            int dx = e.X - cx;
            int dy = e.Y - cy;
            if (dx * dx + dy * dy <= r * r)
            {
                var h = LockClicked;
                if (h != null) h(this, EventArgs.Empty);
            }
        }
    }
}
