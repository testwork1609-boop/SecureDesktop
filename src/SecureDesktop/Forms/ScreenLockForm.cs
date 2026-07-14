using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SecureDesktop.Forms
{
    public class ScreenLockForm : Form
    {
        private readonly Screen _screen;
        private readonly Dictionary<string, Rectangle> _unlockRegions;

        public ScreenLockForm(Screen screen)
        {
            _screen = screen;
            _unlockRegions = new Dictionary<string, Rectangle>();
            InitializeComponent();
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
            this.BackColor = Color.White;
            this.Opacity = 0.10;
            this.AllowTransparency = true;
            this.DoubleBuffered = true;

            var bottomPanel = new Panel
            {
                Location = new Point(0, this.Height - 80),
                Size = new Size(this.Width, 80),
                BackColor = Color.FromArgb(240, 255, 245)
            };

            var lockIcon = new PictureBox
            {
                Size = new Size(50, 50),
                Location = new Point((this.Width / 2) - 25, 15),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var bmp = new Bitmap(50, 50);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(primaryColor))
                {
                    g.FillRectangle(brush, 12, 22, 26, 24);
                }
                using (var pen = new Pen(primaryColor, 4))
                {
                    g.DrawArc(pen, 15, 7, 20, 18, 180, 180);
                }
                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(brush, 20, 30, 10, 6);
                    g.FillRectangle(brush, 23, 34, 4, 8);
                }
            }
            lockIcon.Image = bmp;
            lockIcon.Click += (s, e) => ShowUnlockDialog();

            var helpLabel = new Label
            {
                Text = "Kliknij klodke aby odblokowac",
                Font = new Font("Segoe UI", 10),
                ForeColor = primaryColor,
                Location = new Point((this.Width / 2) - 120, 65),
                AutoSize = true
            };

            bottomPanel.Controls.Add(lockIcon);
            bottomPanel.Controls.Add(helpLabel);

            this.Controls.Add(bottomPanel);
            this.Click += (s, e) => ShowUnlockDialog();
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) ShowUnlockDialog(); };
        }

        public void AddUnlockRegion(string patternKey, Rectangle region)
        {
            _unlockRegions[patternKey] = region;
            UpdateFormRegion();
            this.Invalidate();
        }

        public void RemoveUnlockRegion(string patternKey)
        {
            if (_unlockRegions.Remove(patternKey))
            {
                UpdateFormRegion();
                this.Invalidate();
            }
        }

        public void RemoveAllUnlockRegions()
        {
            if (_unlockRegions.Count > 0)
            {
                _unlockRegions.Clear();
                this.Region = null;
                this.Invalidate();
            }
        }

        private void UpdateFormRegion()
        {
            if (_unlockRegions.Count == 0)
            {
                this.Region = null;
                return;
            }

            try
            {
                var fullRegion = new Region(new Rectangle(0, 0, this.Width, this.Height));
                
                foreach (var rect in _unlockRegions.Values)
                {
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        fullRegion.Exclude(rect);
                    }
                }
                
                this.Region = fullRegion;
            }
            catch
            {
                this.Region = null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            foreach (var kvp in _unlockRegions)
            {
                var region = kvp.Value;
                var name = kvp.Key;

                using (var brush = new SolidBrush(Color.FromArgb(1, 255, 255, 255)))
                {
                    e.Graphics.FillRectangle(brush, region);
                }

                using (var pen = new Pen(Color.FromArgb(255, 45, 165, 90), 3))
                {
                    e.Graphics.DrawRectangle(pen, region);
                }

                using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
                {
                    e.Graphics.DrawString(name, font, Brushes.Green, region.X + 5, region.Y + 5);
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;

            if (m.Msg == WM_NCHITTEST)
            {
                var screenPoint = new Point(m.LParam.ToInt32() & 0xffff, m.LParam.ToInt32() >> 16);
                var clientPoint = this.PointToClient(screenPoint);

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

            var dialog = new Form
            {
                Text = "Odblokuj ekran",
                Size = new Size(350, 200),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                BackColor = Color.White,
                KeyPreview = true
            };

            var icon = new Label { Text = "🔒", Font = new Font("Segoe UI", 24), Location = new Point(20, 20), Size = new Size(50, 40) };
            var title = new Label { Text = "Wprowadz haslo aby odblokowac", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(70, 25), AutoSize = true };
            var passBox = new TextBox { Location = new Point(30, 70), Size = new Size(280, 30), PasswordChar = '*', Font = new Font("Segoe UI", 12) };
            var errorLabel = new Label { Location = new Point(30, 105), Size = new Size(280, 20), ForeColor = Color.Red, Visible = false };

            var unlockBtn = new Button { Text = "Odblokuj", Location = new Point(80, 130), Size = new Size(90, 35), BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            unlockBtn.FlatAppearance.BorderSize = 0;

            var cancelBtn = new Button { Text = "Anuluj", Location = new Point(180, 130), Size = new Size(90, 35), BackColor = Color.White, FlatStyle = FlatStyle.Flat };

            Action unlockAction = () =>
            {
                if (passBox.Text == "admin")
                {
                    dialog.Close();
                    this.Close();
                }
                else
                {
                    errorLabel.Text = "Nieprawidlowe haslo!";
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
}