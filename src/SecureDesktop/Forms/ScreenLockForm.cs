using System;
using System.Drawing;
using System.Windows.Forms;

namespace SecureDesktop.Forms
{
    public class ScreenLockForm : Form
    {
        private readonly Screen _screen;
        private PictureBox _lockIcon;

        public ScreenLockForm(Screen screen)
        {
            _screen = screen;
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
            
            // DELIKATNA biała poświata - tylko lekko przyciemnia
            this.BackColor = Color.White;
            this.Opacity = 0.15; // Tylko 15% krycia - ekran jest prawie widoczny
            this.AllowTransparency = true;
            
            // Ikona kłódki na dole po środku
            _lockIcon = new PictureBox
            {
                Size = new Size(60, 60),
                Location = new Point(
                    (this.Width / 2) - 30,
                    this.Height - 100
                ),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom
            };
            
            // Rysuj ikonę kłódki
            var bmp = new Bitmap(60, 60);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                // Ciało kłódki
                using (var brush = new SolidBrush(Color.FromArgb(200, 50, 50, 50)))
                {
                    g.FillRectangle(brush, 15, 25, 30, 30);
                }
                
                // Pałąk kłódki
                using (var pen = new Pen(Color.FromArgb(200, 50, 50, 50), 5))
                {
                    g.DrawArc(pen, 18, 8, 24, 22, 180, 180);
                }
                
                // Dziurka na klucz
                using (var brush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                {
                    g.FillEllipse(brush, 25, 33, 10, 6);
                    g.FillRectangle(brush, 28, 38, 4, 10);
                }
            }
            _lockIcon.Image = bmp;
            
            // Kliknięcie w kłódkę = odblokowanie
            _lockIcon.Click += (s, e) => ShowUnlockDialog();
            
            // Kliknięcie gdziekolwiek też pokazuje dialog
            this.Click += (s, e) => ShowUnlockDialog();
            
            this.Controls.Add(_lockIcon);
            
            // Blokuj klawiaturę
            this.KeyDown += (s, e) =>
            {
                e.SuppressKeyPress = true;
                // ESC też pokazuje dialog odblokowania
                if (e.KeyCode == Keys.Escape)
                    ShowUnlockDialog();
            };
            
            // Tekst pomocy
            var helpLabel = new Label
            {
                Text = "Kliknij kłódkę lub naciśnij ESC aby odblokować",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(180, 50, 50, 50),
                Location = new Point((this.Width / 2) - 170, this.Height - 40),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom
            };
            this.Controls.Add(helpLabel);
        }

        private void ShowUnlockDialog()
        {
            // Okno wpisywania hasła
            var passwordForm = new Form
            {
                Text = "Odblokuj ekran",
                Size = new Size(350, 200),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                TopMost = true,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White
            };

            var passLabel = new Label
            {
                Text = "Wprowadź hasło:",
                Font = new Font("Segoe UI", 11),
                Location = new Point(20, 30),
                AutoSize = true
            };

            var passBox = new TextBox
            {
                Location = new Point(20, 60),
                Size = new Size(290, 25),
                PasswordChar = '*',
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };

            var errorLabel = new Label
            {
                Location = new Point(20, 95),
                Size = new Size(290, 20),
                ForeColor = Color.Red,
                Visible = false
            };

            var unlockBtn = new Button
            {
                Text = "Odblokuj",
                Location = new Point(80, 120),
                Size = new Size(80, 35),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            var cancelBtn = new Button
            {
                Text = "Anuluj",
                Location = new Point(180, 120),
                Size = new Size(80, 35),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            unlockBtn.Click += (s, args) =>
            {
                // Sprawdź hasło (domyślnie "admin")
                if (passBox.Text == "admin")
                {
                    passwordForm.Close();
                    this.Close(); // Usuń nakładkę
                }
                else
                {
                    errorLabel.Text = "Nieprawidłowe hasło!";
                    errorLabel.Visible = true;
                    passBox.Text = "";
                    passBox.Focus();
                }
            };

            cancelBtn.Click += (s, args) =>
            {
                passwordForm.Close();
            };

            passwordForm.Controls.AddRange(new Control[]
            {
                passLabel, passBox, errorLabel, unlockBtn, cancelBtn
            });

            passwordForm.ShowDialog(this);
        }
    }
}