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
            this.BackColor = Color.FromArgb(200, 0, 0, 0);
            this.Bounds = _screen.Bounds;
            this.Cursor = Cursors.No;

            var lockIcon = new PictureBox
            {
                Size = new Size(40, 40),
                Location = new Point(this.Width - 60, this.Height - 60),
                BackColor = Color.Transparent
            };
            
            Controls.Add(lockIcon);
        }
    }
}