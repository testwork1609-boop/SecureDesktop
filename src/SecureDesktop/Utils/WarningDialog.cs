using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SecureDesktop.Utils
{
    /// <summary>
    /// Modalne okno potwierdzenia z czerwonym piktogramem ostrzeżenia.
    /// Używane np. do potwierdzenia blokady ekranu.
    /// </summary>
    public class WarningDialog : Form
    {
        public static bool Confirm(IWin32Window owner, string title, string message,
                                   string yesText, string noText)
        {
            using (var dlg = new WarningDialog(title, message, yesText, noText))
            {
                return dlg.ShowDialog(owner) == DialogResult.Yes;
            }
        }

        private WarningDialog(string title, string message, string yesText, string noText)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Font = UiFonts.Body;
            this.Size = new Size(460, 300);
            this.Icon = Program.AppIcon;

            // === Czerwony piktogram ostrzeżenia ===
            var iconPanel = new Panel
            {
                Location = new Point(24, 24),
                Size = new Size(64, 64),
                BackColor = Color.Transparent
            };
            iconPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Czerwone koło
                using (var brush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                    e.Graphics.FillEllipse(brush, 0, 0, 64, 64);

                // Wykrzyknik - pionowy słupek
                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillRectangle(brush, 28, 14, 8, 26);
                    // Kropka
                    e.Graphics.FillEllipse(brush, 28, 46, 8, 8);
                }
            };
            this.Controls.Add(iconPanel);

            // === Tytuł ===
            var titleLabel = new Label
            {
                Text = title,
                Font = UiFonts.H2,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(104, 28),
                Size = new Size(330, 30),
                AutoSize = false
            };
            this.Controls.Add(titleLabel);

            // === Treść ===
            var messageLabel = new Label
            {
                Text = message,
                Font = UiFonts.Body,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(104, 66),
                Size = new Size(330, 120),
                AutoSize = false
            };
            this.Controls.Add(messageLabel);

            // === Przyciski ===
            var yesBtn = new Button
            {
                Text = yesText,
                DialogResult = DialogResult.Yes,
                Location = new Point(90, 210),
                Size = new Size(140, 42),
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = UiFonts.BodyBold,
                Cursor = Cursors.Hand
            };
            yesBtn.FlatAppearance.BorderSize = 0;
            this.Controls.Add(yesBtn);

            var noBtn = new Button
            {
                Text = noText,
                DialogResult = DialogResult.No,
                Location = new Point(240, 210),
                Size = new Size(140, 42),
                BackColor = Color.White,
                ForeColor = UiTheme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = UiFonts.Body,
                Cursor = Cursors.Hand
            };
            noBtn.FlatAppearance.BorderColor = UiTheme.BorderStrong;
            noBtn.FlatAppearance.BorderSize = 1;
            this.Controls.Add(noBtn);

            this.AcceptButton = noBtn;   // Enter = bezpieczna opcja (Anuluj)
            this.CancelButton = noBtn;
        }
    }
}
