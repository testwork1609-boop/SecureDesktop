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
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = _screen.Bounds;
            this.Cursor = Cursors.No;
            
            // Użyj CZARNEGO zamiast przezroczystego
            this.BackColor = Color.Black;
            this.Opacity = 0.9;
            
            // Ikona kłódki na dole
            var lockIcon = new PictureBox
            {
                Size = new Size(50, 50),
                Location = new Point(this.Width - 70, this.Height - 70),
                BackColor = Color.FromArgb(50, 50, 50),
                Cursor = Cursors.Hand
            };
            
            // Rysuj kłódkę
            var bmp = new Bitmap(50, 50);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(Brushes.Gray, 10, 5, 30, 30);
                g.FillRectangle(Brushes.Gray, 15, 20, 20, 25);
                g.FillEllipse(Brushes.Black, 20, 28, 10, 8);
            }
            lockIcon.Image = bmp;
            lockIcon.Click += (s, e) => this.Close();
            
            this.Controls.Add(lockIcon);
            
            // Blokuj klawiaturę
            this.KeyDown += (s, e) => e.SuppressKeyPress = true;
        }
    }
}