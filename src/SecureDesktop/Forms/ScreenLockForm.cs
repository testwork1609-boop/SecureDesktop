using System;
using System.Drawing;
using System.Windows.Forms;

namespace SecureDesktop.Forms
{
    public class ScreenLockForm : Form
    {
        private readonly Screen _screen;

        public ScreenLockForm(Screen screen)
        {
            _screen = screen;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Color primaryColor = Color.FromArgb(45, 165, 90); // #2DA55A

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = _screen.Bounds;
            this.Cursor = Cursors.No;
            
            // Delikatna biała poświata
            this.BackColor = Color.White;
            this.Opacity = 0.12;
            this.AllowTransparency = true;
            
            // Panel na dole
            var bottomPanel = new Panel
            {
                Location = new Point(0, this.Height - 80),
                Size = new Size(this.Width, 80),
                BackColor = Color.FromArgb(240, 255, 245)
            };

            // Ikona kłódki
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
                
                // Kłódka
                using (var brush = new SolidBrush(primaryColor))
                {
                    g.FillRectangle(brush, 12, 22, 26, 24);
                }
                using (var pen = new Pen(primaryColor, 4))
                {
                    g.DrawArc(pen, 15, 7, 20, 18, 180, 180);
                }
                // Dziurka
                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(brush, 20, 30, 10, 6);
                    g.FillRectangle(brush, 23, 34, 4, 8);
                }
            }
            lockIcon.Image = bmp;
            lockIcon.Click += (s, e) => ShowUnlockDialog();

            // Tekst
            var helpLabel = new Label
            {
                Text = "Kliknij kłódkę aby odblokować",
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
                BackColor = Color.White
            };

            var icon = new Label
            {
                Text = "🔒",
                Font = new Font("Segoe UI", 24),
                Location = new Point(20, 20),
                Size = new Size(50, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var title = new Label
            {
                Text = "Wprowadź hasło aby odblokować",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(70, 25),
                AutoSize = true,
                ForeColor = Color.FromArgb(30, 30, 30)
            };

            var passBox = new TextBox
            {
                Location = new Point(30, 70),
                Size = new Size(280, 30),
                PasswordChar = '●',
                Font = new Font("Segoe UI", 12),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(245, 245, 245)
            };

            var errorLabel = new Label
            {
                Location = new Point(30, 105),
                Size = new Size(280, 20),
                ForeColor = Color.Red,
                Visible = false
            };

            var unlockBtn = new Button
            {
                Text = "Odblokuj",
                Location = new Point(80, 130),
                Size = new Size(90, 35),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            unlockBtn.FlatAppearance.BorderSize = 0;
            unlockBtn.Click += (s, args) =>
            {
                if (passBox.Text == "admin")
                {
                    dialog.Close();
                    this.Close();
                }
                else
                {
                    errorLabel.Text = "Nieprawidłowe hasło!";
                    errorLabel.Visible = true;
                    passBox.Text = "";
                }
            };

            var cancelBtn = new Button
            {
                Text = "Anuluj",
                Location = new Point(180, 130),
                Size = new Size(90, 35),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(100, 100, 100),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.Click += (s, args) => dialog.Close();

            dialog.Controls.AddRange(new Control[] { icon, title, passBox, errorLabel, unlockBtn, cancelBtn });
            dialog.ShowDialog(this);
        }
    }
}