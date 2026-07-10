using System;
using System.Drawing;
using System.Windows.Forms;

namespace SecureDesktop.Forms
{
    public class ConfigurationForm : Form
    {
        private TextBox _backupPathBox;
        private TextBox _checkpointPathBox;
        private TextBox _checkpointArgsBox;
        private TextBox _adminPasswordBox;

        public ConfigurationForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Konfiguracja SecureDesktop";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int yPos = 20;

            // Tytuł
            var title = new Label
            {
                Text = "Konfiguracja aplikacji",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 40;

            // Hasło administratora
            var passLabel = new Label
            {
                Text = "Hasło administratora:",
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 25;

            _adminPasswordBox = new TextBox
            {
                Location = new Point(20, yPos),
                Size = new Size(300, 25),
                PasswordChar = '*',
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Text = "admin"
            };
            yPos += 40;

            // Ścieżka backupu
            var backupLabel = new Label
            {
                Text = "Folder kopii zapasowych:",
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 25;

            _backupPathBox = new TextBox
            {
                Location = new Point(20, yPos),
                Size = new Size(400, 25),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Text = ".\\Backup"
            };
            
            var backupBrowseBtn = new Button
            {
                Text = "...",
                Location = new Point(430, yPos),
                Size = new Size(40, 25),
                BackColor = Color.FromArgb(0, 120, 212)
            };
            backupBrowseBtn.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                        _backupPathBox.Text = dialog.SelectedPath;
                }
            };
            yPos += 40;

            // CheckPoint - ścieżka EXE
            var checkpointLabel = new Label
            {
                Text = "Aplikacja CheckPoint (EXE):",
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 25;

            _checkpointPathBox = new TextBox
            {
                Location = new Point(20, yPos),
                Size = new Size(400, 25),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Text = "notepad.exe"
            };
            
            var checkpointBrowseBtn = new Button
            {
                Text = "...",
                Location = new Point(430, yPos),
                Size = new Size(40, 25),
                BackColor = Color.FromArgb(0, 120, 212)
            };
            checkpointBrowseBtn.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Pliki EXE|*.exe|Wszystkie pliki|*.*";
                    if (dialog.ShowDialog() == DialogResult.OK)
                        _checkpointPathBox.Text = dialog.FileName;
                }
            };
            yPos += 40;

            // CheckPoint - argumenty
            var argsLabel = new Label
            {
                Text = "Parametry uruchomienia:",
                Location = new Point(20, yPos),
                AutoSize = true
            };
            yPos += 25;

            _checkpointArgsBox = new TextBox
            {
                Location = new Point(20, yPos),
                Size = new Size(400, 25),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            yPos += 50;

            // Przyciski
            var saveBtn = new Button
            {
                Text = "Zapisz",
                Location = new Point(150, yPos),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(0, 120, 212),
                FlatStyle = FlatStyle.Flat
            };
            saveBtn.Click += (s, e) =>
            {
                MessageBox.Show("Ustawienia zapisane!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };

            var cancelBtn = new Button
            {
                Text = "Anuluj",
                Location = new Point(300, yPos),
                Size = new Size(120, 40),
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat
            };
            cancelBtn.Click += (s, e) => this.Close();

            Controls.AddRange(new Control[]
            {
                title,
                passLabel, _adminPasswordBox,
                backupLabel, _backupPathBox, backupBrowseBtn,
                checkpointLabel, _checkpointPathBox, checkpointBrowseBtn,
                argsLabel, _checkpointArgsBox,
                saveBtn, cancelBtn
            });
        }
    }
}