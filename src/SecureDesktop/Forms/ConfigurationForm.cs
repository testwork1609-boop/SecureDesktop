using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Forms
{
    public class ConfigurationForm : Form
    {
        private TextBox _backupPathBox;
        private TextBox _checkpointPathBox;
        private TextBox _checkpointArgsBox;
        private TextBox _adminPasswordBox;
        private ListBox _patternListBox;
        private List<Pattern> _patterns;

        public ConfigurationForm()
        {
            _patterns = new List<Pattern>();
            InitializeComponent();
            LoadSamplePatterns();
        }

        private void InitializeComponent()
        {
            this.Text = "Konfiguracja SecureDesktop";
            this.Size = new Size(750, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Tytuł
            var title = new Label
            {
                Text = "Konfiguracja aplikacji",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };

            // Zakładki
            var tabControl = new TabControl
            {
                Location = new Point(10, 50),
                Size = new Size(710, 540),
                Appearance = TabAppearance.FlatButtons,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            // === ZAKŁADKA 1: Ogólne ===
            var tabGeneral = new TabPage
            {
                Text = "  Ogólne  ",
                BackColor = Color.FromArgb(40, 40, 45)
            };
            AddGeneralSettings(tabGeneral);

            // === ZAKŁADKA 2: Patterny ===
            var tabPatterns = new TabPage
            {
                Text = "  Patterny  ",
                BackColor = Color.FromArgb(40, 40, 45)
            };
            AddPatternSettings(tabPatterns);

            // === ZAKŁADKA 3: CheckPoint ===
            var tabCheckpoint = new TabPage
            {
                Text = "  CheckPoint  ",
                BackColor = Color.FromArgb(40, 40, 45)
            };
            AddCheckpointSettings(tabCheckpoint);

            tabControl.TabPages.Add(tabGeneral);
            tabControl.TabPages.Add(tabPatterns);
            tabControl.TabPages.Add(tabCheckpoint);

            // Przyciski na dole
            var saveBtn = new Button
            {
                Text = "Zapisz ustawienia",
                Location = new Point(250, 600),
                Size = new Size(150, 40),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            saveBtn.Click += (s, e) =>
            {
                MessageBox.Show("Ustawienia zapisane!", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };

            var cancelBtn = new Button
            {
                Text = "Anuluj",
                Location = new Point(420, 600),
                Size = new Size(100, 40),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cancelBtn.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { title, tabControl, saveBtn, cancelBtn });
        }

        private void AddGeneralSettings(TabPage tab)
        {
            int y = 20;

            // Hasło admina
            var passLabel = new Label
            {
                Text = "Hasło administratora:",
                Location = new Point(20, y),
                AutoSize = true
            };
            _adminPasswordBox = new TextBox
            {
                Location = new Point(200, y),
                Size = new Size(200, 25),
                PasswordChar = '*',
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White,
                Text = "admin"
            };
            y += 40;

            // Folder backup
            var backupLabel = new Label
            {
                Text = "Folder kopii zapasowych:",
                Location = new Point(20, y),
                AutoSize = true
            };
            _backupPathBox = new TextBox
            {
                Location = new Point(200, y),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White,
                Text = ".\\Backup"
            };
            var backupBtn = new Button
            {
                Text = "...",
                Location = new Point(510, y),
                Size = new Size(40, 25),
                BackColor = Color.FromArgb(0, 120, 212)
            };
            backupBtn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                        _backupPathBox.Text = dlg.SelectedPath;
                }
            };
            y += 40;

            // Autostart
            var autoStartCheck = new CheckBox
            {
                Text = "Uruchamiaj z systemem Windows",
                Location = new Point(20, y),
                AutoSize = true
            };
            y += 30;

            // Minimalizacja do tray
            var trayCheck = new CheckBox
            {
                Text = "Minimalizuj do zasobnika systemowego",
                Location = new Point(20, y),
                AutoSize = true,
                Checked = true
            };

            tab.Controls.AddRange(new Control[]
            {
                passLabel, _adminPasswordBox,
                backupLabel, _backupPathBox, backupBtn,
                autoStartCheck, trayCheck
            });
        }

        private void AddPatternSettings(TabPage tab)
        {
            int y = 20;

            var infoLabel = new Label
            {
                Text = "Zarządzanie wzorcami ekranowymi (Pattern):",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true
            };
            y += 35;

            // Lista patternów
            _patternListBox = new ListBox
            {
                Location = new Point(20, y),
                Size = new Size(400, 250),
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10)
            };
            y += 260;

            // Przyciski zarządzania
            var addBtn = new Button
            {
                Text = "Dodaj Pattern",
                Location = new Point(20, y),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(0, 120, 212),
                FlatStyle = FlatStyle.Flat
            };
            addBtn.Click += (s, e) => AddNewPattern();
            y += 45;

            var deleteBtn = new Button
            {
                Text = "Usuń zaznaczony",
                Location = new Point(20, y),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(180, 50, 50),
                FlatStyle = FlatStyle.Flat
            };
            deleteBtn.Click += (s, e) =>
            {
                if (_patternListBox.SelectedIndex >= 0)
                {
                    _patterns.RemoveAt(_patternListBox.SelectedIndex);
                    RefreshPatternList();
                }
            };

            // Opcje patternów
            var thresholdLabel = new Label
            {
                Text = "Próg zgodności (%):",
                Location = new Point(450, 30),
                AutoSize = true
            };
            var thresholdBox = new NumericUpDown
            {
                Location = new Point(450, 55),
                Size = new Size(80, 25),
                Minimum = 50,
                Maximum = 100,
                Value = 95,
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White
            };

            var intervalLabel = new Label
            {
                Text = "Interwał skanowania (ms):",
                Location = new Point(450, 90),
                AutoSize = true
            };
            var intervalBox = new NumericUpDown
            {
                Location = new Point(450, 115),
                Size = new Size(80, 25),
                Minimum = 100,
                Maximum = 5000,
                Value = 500,
                Increment = 100,
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White
            };

            var marginLabel = new Label
            {
                Text = "Margines bezpieczeństwa (px):",
                Location = new Point(450, 150),
                AutoSize = true
            };
            var marginBox = new NumericUpDown
            {
                Location = new Point(450, 175),
                Size = new Size(80, 25),
                Minimum = 0,
                Maximum = 100,
                Value = 10,
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White
            };

            tab.Controls.AddRange(new Control[]
            {
                infoLabel, _patternListBox, addBtn, deleteBtn,
                thresholdLabel, thresholdBox,
                intervalLabel, intervalBox,
                marginLabel, marginBox
            });
        }

        private void AddCheckpointSettings(TabPage tab)
        {
            int y = 20;

            var infoLabel = new Label
            {
                Text = "Konfiguracja aplikacji CheckPoint:",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true
            };
            y += 40;

            var pathLabel = new Label
            {
                Text = "Ścieżka do pliku EXE:",
                Location = new Point(20, y),
                AutoSize = true
            };
            _checkpointPathBox = new TextBox
            {
                Location = new Point(200, y),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White,
                Text = "notepad.exe"
            };
            var browseBtn = new Button
            {
                Text = "Przeglądaj",
                Location = new Point(510, y),
                Size = new Size(80, 25),
                BackColor = Color.FromArgb(0, 120, 212)
            };
            browseBtn.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Pliki EXE|*.exe|Wszystkie pliki|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                        _checkpointPathBox.Text = dlg.FileName;
                }
            };
            y += 40;

            var argsLabel = new Label
            {
                Text = "Parametry uruchomienia:",
                Location = new Point(20, y),
                AutoSize = true
            };
            _checkpointArgsBox = new TextBox
            {
                Location = new Point(200, y),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White
            };
            y += 40;

            var workDirLabel = new Label
            {
                Text = "Katalog roboczy:",
                Location = new Point(20, y),
                AutoSize = true
            };
            var workDirBox = new TextBox
            {
                Location = new Point(200, y),
                Size = new Size(300, 25),
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White
            };
            var workDirBtn = new Button
            {
                Text = "...",
                Location = new Point(510, y),
                Size = new Size(40, 25),
                BackColor = Color.FromArgb(0, 120, 212)
            };
            workDirBtn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                        workDirBox.Text = dlg.SelectedPath;
                }
            };

            tab.Controls.AddRange(new Control[]
            {
                infoLabel,
                pathLabel, _checkpointPathBox, browseBtn,
                argsLabel, _checkpointArgsBox,
                workDirLabel, workDirBox, workDirBtn
            });
        }

        private void LoadSamplePatterns()
        {
            _patterns.Add(new Pattern
            {
                Id = 1,
                Name = "Przycisk Start",
                Description = "Przycisk Start w Windows",
                MarginTop = 5,
                MarginBottom = 5,
                MarginLeft = 10,
                MarginRight = 10,
                IsActive = true,
                CreatedAt = DateTime.Now
            });

            _patterns.Add(new Pattern
            {
                Id = 2,
                Name = "Pasek zadań",
                Description = "Dolny pasek zadań",
                MarginTop = 2,
                MarginBottom = 2,
                MarginLeft = 0,
                MarginRight = 0,
                IsActive = false,
                CreatedAt = DateTime.Now
            });

            RefreshPatternList();
        }

        private void RefreshPatternList()
        {
            _patternListBox.Items.Clear();
            foreach (var p in _patterns)
            {
                _patternListBox.Items.Add(
                    $"{(p.IsActive ? "✓" : "✗")} {p.Name} | Margines: {p.MarginLeft}/{p.MarginTop}/{p.MarginRight}/{p.MarginBottom} | {p.Description}"
                );
            }
        }

        private void AddNewPattern()
        {
            var dialog = new Form
            {
                Text = "Dodaj nowy Pattern",
                Size = new Size(400, 350),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };

            int y = 20;
            var nameLabel = new Label { Text = "Nazwa:", Location = new Point(20, y), AutoSize = true };
            var nameBox = new TextBox { Location = new Point(20, y + 25), Size = new Size(340, 25), BackColor = Color.FromArgb(50, 50, 55), ForeColor = Color.White };
            y += 60;

            var descLabel = new Label { Text = "Opis:", Location = new Point(20, y), AutoSize = true };
            var descBox = new TextBox { Location = new Point(20, y + 25), Size = new Size(340, 25), BackColor = Color.FromArgb(50, 50, 55), ForeColor = Color.White };
            y += 60;

            var marginLabel = new Label { Text = "Margines bezpieczeństwa:", Location = new Point(20, y), AutoSize = true };
            var marginBox = new NumericUpDown { Location = new Point(20, y + 25), Size = new Size(80, 25), Value = 10, BackColor = Color.FromArgb(50, 50, 55), ForeColor = Color.White };
            y += 60;

            var activeCheck = new CheckBox { Text = "Aktywny", Location = new Point(20, y), Checked = true };
            y += 40;

            var saveBtn = new Button { Text = "Dodaj", Location = new Point(120, y), Size = new Size(100, 35), BackColor = Color.FromArgb(0, 120, 212), FlatStyle = FlatStyle.Flat };
            saveBtn.Click += (s, args) =>
            {
                _patterns.Add(new Pattern
                {
                    Id = _patterns.Count + 1,
                    Name = nameBox.Text,
                    Description = descBox.Text,
                    MarginTop = (int)marginBox.Value,
                    MarginBottom = (int)marginBox.Value,
                    MarginLeft = (int)marginBox.Value,
                    MarginRight = (int)marginBox.Value,
                    IsActive = activeCheck.Checked,
                    CreatedAt = DateTime.Now
                });
                RefreshPatternList();
                dialog.Close();
            };

            dialog.Controls.AddRange(new Control[] { nameLabel, nameBox, descLabel, descBox, marginLabel, marginBox, activeCheck, saveBtn });
            dialog.ShowDialog(this);
        }
    }
}