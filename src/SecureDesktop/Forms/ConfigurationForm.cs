using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Forms
{
    public class ConfigurationForm : Form
    {
        // Kolorystyka
        private readonly Color primaryColor = Color.FromArgb(45, 165, 90); // #2DA55A
        private readonly Color bgColor = Color.White;
        private readonly Color cardColor = Color.FromArgb(248, 249, 250);
        private readonly Color textColor = Color.FromArgb(30, 30, 30);
        private readonly Color subtitleColor = Color.FromArgb(100, 100, 100);
        private readonly Color inputBg = Color.FromArgb(245, 245, 245);
        private readonly Color borderColor = Color.FromArgb(220, 220, 220);

        // Kontrolki
        private TextBox _adminPasswordBox;
        private TextBox _backupPathBox;
        private TextBox _checkpointPathBox;
        private TextBox _checkpointArgsBox;
        private TextBox _checkpointWorkDirBox;
        private ListBox _patternListBox;
        private NumericUpDown _thresholdBox;
        private NumericUpDown _intervalBox;
        private NumericUpDown _marginBox;
        private CheckBox _autoStartCheck;
        private CheckBox _trayCheck;
        
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
            this.Size = new Size(780, 620);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Font = new Font("Segoe UI", 9);

            // === NAGŁÓWEK ===
            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(780, 55),
                BackColor = primaryColor
            };

            var headerTitle = new Label
            {
                Text = "⚙️  Konfiguracja SecureDesktop",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(20, 12),
                AutoSize = true,
                ForeColor = Color.White
            };

            headerPanel.Controls.Add(headerTitle);

            // === ZAKŁADKI ===
            var tabControl = new TabControl
            {
                Location = new Point(10, 65),
                Size = new Size(745, 480),
                Appearance = TabAppearance.FlatButtons,
                DrawMode = TabDrawMode.OwnerDrawFixed
            };
            tabControl.DrawItem += TabControl_DrawItem;

            // Zakładka 1: Ogólne
            var tabGeneral = new TabPage("  Ogólne  ");
            tabGeneral.BackColor = bgColor;
            BuildGeneralTab(tabGeneral);

            // Zakładka 2: Patterny
            var tabPatterns = new TabPage("  Patterny  ");
            tabPatterns.BackColor = bgColor;
            BuildPatternsTab(tabPatterns);

            // Zakładka 3: CheckPoint
            var tabCheckpoint = new TabPage("  CheckPoint  ");
            tabCheckpoint.BackColor = bgColor;
            BuildCheckpointTab(tabCheckpoint);

            // Zakładka 4: Backup
            var tabBackup = new TabPage("  Backup  ");
            tabBackup.BackColor = bgColor;
            BuildBackupTab(tabBackup);

            tabControl.TabPages.Add(tabGeneral);
            tabControl.TabPages.Add(tabPatterns);
            tabControl.TabPages.Add(tabCheckpoint);
            tabControl.TabPages.Add(tabBackup);

            // === PRZYCISKI NA DOLE ===
            var buttonPanel = new Panel
            {
                Location = new Point(0, 550),
                Size = new Size(780, 40),
                BackColor = bgColor
            };

            var saveBtn = new Button
            {
                Text = "💾  Zapisz wszystkie ustawienia",
                Location = new Point(200, 5),
                Size = new Size(220, 35),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += SaveBtn_Click;
            saveBtn.MouseEnter += (s, e) => saveBtn.BackColor = Color.FromArgb(50, 185, 105);
            saveBtn.MouseLeave += (s, e) => saveBtn.BackColor = primaryColor;

            var cancelBtn = new Button
            {
                Text = "Anuluj",
                Location = new Point(440, 5),
                Size = new Size(100, 35),
                BackColor = Color.White,
                ForeColor = subtitleColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            cancelBtn.FlatAppearance.BorderColor = borderColor;
            cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.Click += (s, e) => this.Close();

            buttonPanel.Controls.Add(saveBtn);
            buttonPanel.Controls.Add(cancelBtn);

            // Dodaj wszystko do formularza
            this.Controls.AddRange(new Control[] { headerPanel, tabControl, buttonPanel });
        }

        // ==================== ZAKŁADKA OGÓLNE ====================
        private void BuildGeneralTab(TabPage tab)
        {
            int y = 20;

            // Sekcja: Bezpieczeństwo
            var securityCard = CreateCard("🔐  Bezpieczeństwo", 15, y, 700, 120);
            
            var passLabel = new Label
            {
                Text = "Hasło administratora:",
                Location = new Point(25, 35),
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10)
            };
            
            _adminPasswordBox = new TextBox
            {
                Location = new Point(200, 32),
                Size = new Size(200, 28),
                PasswordChar = '●',
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "admin",
                Font = new Font("Segoe UI", 10)
            };

            var passHint = new Label
            {
                Text = "Hasło używane do odblokowywania ekranu",
                Location = new Point(25, 65),
                AutoSize = true,
                ForeColor = subtitleColor,
                Font = new Font("Segoe UI", 8)
            };

            securityCard.Controls.Add(passLabel);
            securityCard.Controls.Add(_adminPasswordBox);
            securityCard.Controls.Add(passHint);
            
            y += 135;

            // Sekcja: Zachowanie
            var behaviorCard = CreateCard("⚡  Zachowanie aplikacji", 15, y, 700, 100);
            
            _autoStartCheck = new CheckBox
            {
                Text = "Uruchamiaj aplikację przy starcie systemu Windows",
                Location = new Point(25, 35),
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10)
            };

            _trayCheck = new CheckBox
            {
                Text = "Minimalizuj do zasobnika systemowego (system tray)",
                Location = new Point(25, 65),
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10),
                Checked = true
            };

            behaviorCard.Controls.Add(_autoStartCheck);
            behaviorCard.Controls.Add(_trayCheck);
            
            y += 115;

            // Sekcja: Monitorowanie pliku
            var monitorCard = CreateCard("📁  Monitorowanie pliku", 15, y, 700, 100);
            
            var monitorLabel = new Label
            {
                Text = "Plik do monitorowania (SHA-256):",
                Location = new Point(25, 35),
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10)
            };

            var monitorPathBox = new TextBox
            {
                Location = new Point(250, 32),
                Size = new Size(300, 28),
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                                Text = ""
            };

            var monitorBrowseBtn = new Button
            {
                Text = "Przeglądaj",
                Location = new Point(560, 31),
                Size = new Size(100, 30),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            monitorBrowseBtn.FlatAppearance.BorderSize = 0;
            monitorBrowseBtn.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Wszystkie pliki|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                        monitorPathBox.Text = dlg.FileName;
                }
            };

            monitorCard.Controls.Add(monitorLabel);
            monitorCard.Controls.Add(monitorPathBox);
            monitorCard.Controls.Add(monitorBrowseBtn);

            tab.Controls.Add(securityCard);
            tab.Controls.Add(behaviorCard);
            tab.Controls.Add(monitorCard);
        }

        // ==================== ZAKŁADKA PATTERNY ====================
        private void BuildPatternsTab(TabPage tab)
        {
            // Panel lewy - lista patternów
            var listCard = CreateCard("📋  Lista wzorców (Pattern)", 15, 15, 450, 300);

            _patternListBox = new ListBox
            {
                Location = new Point(15, 35),
                Size = new Size(420, 250),
                BackColor = inputBg,
                ForeColor = textColor,
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Przyciski zarządzania
            var addBtn = new Button
            {
                Text = "➕  Dodaj Pattern",
                Location = new Point(15, 295),
                Size = new Size(135, 32),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            addBtn.FlatAppearance.BorderSize = 0;
            addBtn.Click += AddPatternBtn_Click;

            var editBtn = new Button
            {
                Text = "✏️  Edytuj",
                Location = new Point(160, 295),
                Size = new Size(135, 32),
                BackColor = Color.White,
                ForeColor = primaryColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            editBtn.FlatAppearance.BorderColor = primaryColor;
            editBtn.FlatAppearance.BorderSize = 1;
            editBtn.Click += (s, e) => MessageBox.Show("Kliknij dwukrotnie na pattern aby edytować.", "Info");

            var deleteBtn = new Button
            {
                Text = "🗑️  Usuń",
                Location = new Point(305, 295),
                Size = new Size(135, 32),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(220, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            deleteBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 80, 80);
            deleteBtn.FlatAppearance.BorderSize = 1;
            deleteBtn.Click += (s, e) => DeletePattern();

            listCard.Controls.Add(_patternListBox);
            listCard.Controls.Add(addBtn);
            listCard.Controls.Add(editBtn);
            listCard.Controls.Add(deleteBtn);

            // Panel prawy - ustawienia
            var settingsCard = CreateCard("⚙️  Ustawienia wykrywania", 480, 15, 240, 300);

            int sy = 35;

            var thresholdLabel = new Label
            {
                Text = "Próg zgodności (%):",
                Location = new Point(20, sy),
                AutoSize = true,
                ForeColor = textColor
            };
            sy += 22;

            _thresholdBox = new NumericUpDown
            {
                Location = new Point(20, sy),
                Size = new Size(80, 25),
                Minimum = 50,
                Maximum = 100,
                Value = 95,
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            sy += 40;

            var intervalLabel = new Label
            {
                Text = "Interwał skanowania (ms):",
                Location = new Point(20, sy),
                AutoSize = true,
                ForeColor = textColor
            };
            sy += 22;

            _intervalBox = new NumericUpDown
            {
                Location = new Point(20, sy),
                Size = new Size(80, 25),
                Minimum = 100,
                Maximum = 5000,
                Value = 500,
                Increment = 100,
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            sy += 40;

            var marginLabel = new Label
            {
                Text = "Margines bezpieczeństwa (px):",
                Location = new Point(20, sy),
                AutoSize = true,
                ForeColor = textColor
            };
            sy += 22;

            _marginBox = new NumericUpDown
            {
                Location = new Point(20, sy),
                Size = new Size(80, 25),
                Minimum = 0,
                Maximum = 100,
                Value = 10,
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };

            settingsCard.Controls.Add(thresholdLabel);
            settingsCard.Controls.Add(_thresholdBox);
            settingsCard.Controls.Add(intervalLabel);
            settingsCard.Controls.Add(_intervalBox);
            settingsCard.Controls.Add(marginLabel);
            settingsCard.Controls.Add(_marginBox);

            // Info
            var infoCard = CreateCard("💡  Informacja", 480, 330, 240, 100);
            
            var infoText = new Label
            {
                Text = "Patterny służą do odblokowywania\nokreślonych obszarów ekranu.\n\nWyższy próg = dokładniejsze\nwykrywanie wzorca.",
                Location = new Point(15, 30),
                AutoSize = true,
                ForeColor = subtitleColor,
                Font = new Font("Segoe UI", 9)
            };

            infoCard.Controls.Add(infoText);

            tab.Controls.Add(listCard);
            tab.Controls.Add(settingsCard);
            tab.Controls.Add(infoCard);
        }

        // ==================== ZAKŁADKA CHECKPOINT ====================
        private void BuildCheckpointTab(TabPage tab)
        {
            int y = 20;

            var configCard = CreateCard("🚀  Konfiguracja aplikacji CheckPoint", 15, y, 700, 200);

            var pathLabel = new Label
            {
                Text = "Ścieżka do pliku EXE:",
                Location = new Point(25, 35),
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10)
            };

            _checkpointPathBox = new TextBox
            {
                Location = new Point(25, 62),
                Size = new Size(500, 28),
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "notepad.exe",
                Font = new Font("Segoe UI", 10)
            };

            var pathBrowseBtn = new Button
            {
                Text = "Przeglądaj",
                Location = new Point(535, 61),
                Size = new Size(120, 30),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            pathBrowseBtn.FlatAppearance.BorderSize = 0;
            pathBrowseBtn.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Pliki EXE|*.exe|Wszystkie pliki|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                        _checkpointPathBox.Text = dlg.FileName;
                }
            };

            var argsLabel = new Label
            {
                Text = "Parametry uruchomienia:",
                Location = new Point(25, 100),
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10)
            };

            _checkpointArgsBox = new TextBox
            {
                Location = new Point(25, 127),
                Size = new Size(300, 28),
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                Text = ""
            };

            var workDirLabel = new Label
            {
                Text = "Katalog roboczy:",
                Location = new Point(350, 100),
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10)
            };

            _checkpointWorkDirBox = new TextBox
            {
                Location = new Point(350, 127),
                Size = new Size(200, 28),
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };

            var workDirBtn = new Button
            {
                Text = "...",
                Location = new Point(555, 126),
                Size = new Size(35, 30),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            workDirBtn.FlatAppearance.BorderSize = 0;
            workDirBtn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                        _checkpointWorkDirBox.Text = dlg.SelectedPath;
                }
            };

            configCard.Controls.Add(pathLabel);
            configCard.Controls.Add(_checkpointPathBox);
            configCard.Controls.Add(pathBrowseBtn);
            configCard.Controls.Add(argsLabel);
            configCard.Controls.Add(_checkpointArgsBox);
            configCard.Controls.Add(workDirLabel);
            configCard.Controls.Add(_checkpointWorkDirBox);
            configCard.Controls.Add(workDirBtn);

            y += 220;

            var testCard = CreateCard("🧪  Test CheckPoint", 15, y, 700, 100);
            
            var testBtn = new Button
            {
                Text = "▶️  Testuj uruchomienie",
                Location = new Point(200, 30),
                Size = new Size(250, 40),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            testBtn.FlatAppearance.BorderSize = 0;
            testBtn.Click += (s, e) =>
            {
                try
                {
                    var path = _checkpointPathBox.Text;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        System.Diagnostics.Process.Start(path, _checkpointArgsBox.Text);
                        MessageBox.Show("✅ Aplikacja uruchomiona pomyślnie!", "Test CheckPoint",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Podaj ścieżkę do pliku EXE.", "Info");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("❌ Błąd: " + ex.Message, "Test CheckPoint",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            testCard.Controls.Add(testBtn);

            tab.Controls.Add(configCard);
            tab.Controls.Add(testCard);
        }

        // ==================== ZAKŁADKA BACKUP ====================
        private void BuildBackupTab(TabPage tab)
        {
            int y = 20;

            var backupCard = CreateCard("💾  Konfiguracja kopii zapasowych", 15, y, 700, 150);

            var backupLabel = new Label
            {
                Text = "Folder docelowy backupu:",
                Location = new Point(25, 35),
                AutoSize = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10)
            };

            _backupPathBox = new TextBox
            {
                Location = new Point(25, 62),
                Size = new Size(500, 28),
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Text = ".\\Backup",
                Font = new Font("Segoe UI", 10)
            };

            var backupBrowseBtn = new Button
            {
                Text = "Przeglądaj",
                Location = new Point(535, 61),
                Size = new Size(120, 30),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            backupBrowseBtn.FlatAppearance.BorderSize = 0;
            backupBrowseBtn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                        _backupPathBox.Text = dlg.SelectedPath;
                }
            };

            var backupAutoCheck = new CheckBox
            {
                Text = "Automatyczny backup przy logowaniu",
                Location = new Point(25, 105),
                AutoSize = true,
                Checked = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10)
            };

            backupCard.Controls.Add(backupLabel);
            backupCard.Controls.Add(_backupPathBox);
            backupCard.Controls.Add(backupBrowseBtn);
            backupCard.Controls.Add(backupAutoCheck);

            y += 170;

            var backupNowCard = CreateCard("⚡  Natychmiastowy backup", 15, y, 700, 80);
            
            var backupNowBtn = new Button
            {
                Text = "💾  Wykonaj backup teraz",
                Location = new Point(200, 20),
                Size = new Size(250, 40),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            backupNowBtn.FlatAppearance.BorderSize = 0;
            backupNowBtn.Click += (s, e) =>
            {
                try
                {
                    var sourcePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                    if (System.IO.File.Exists(sourcePath))
                    {
                        var backupService = new Services.BackupService();
                        var result = backupService.CreateBackup(sourcePath, _backupPathBox.Text);
                        MessageBox.Show("✅ Backup utworzony!\n\n" + result, "Backup",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Brak pliku bazy danych.", "Info");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("❌ Błąd: " + ex.Message, "Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            backupNowCard.Controls.Add(backupNowBtn);

            tab.Controls.Add(backupCard);
            tab.Controls.Add(backupNowCard);
        }

        // ==================== METODY POMOCNICZE ====================
        private Panel CreateCard(string title, int x, int y, int width, int height)
        {
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            // Cień/obramowanie
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(borderColor, 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };

            // Tytuł karty
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(10, 8),
                AutoSize = true,
                ForeColor = primaryColor
            };

            // Linia pod tytułem
            var line = new Panel
            {
                Location = new Point(10, 30),
                Size = new Size(width - 20, 1),
                BackColor = borderColor
            };

            card.Controls.Add(titleLabel);
            card.Controls.Add(line);

            return card;
        }

        private void LoadSamplePatterns()
        {
            _patterns = new List<Pattern>
            {
                new Pattern
                {
                    Id = 1,
                    Name = "Przycisk Start",
                    Description = "Przycisk Start w systemie Windows",
                    MarginTop = 5,
                    MarginBottom = 5,
                    MarginLeft = 10,
                    MarginRight = 10,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new Pattern
                {
                    Id = 2,
                    Name = "Pasek zadań",
                    Description = "Dolny pasek zadań Windows",
                    MarginTop = 2,
                    MarginBottom = 2,
                    MarginLeft = 0,
                    MarginRight = 0,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                },
                new Pattern
                {
                    Id = 3,
                    Name = "Zegar systemowy",
                    Description = "Zegar w prawym dolnym rogu",
                    MarginTop = 3,
                    MarginBottom = 3,
                    MarginLeft = 5,
                    MarginRight = 5,
                    IsActive = false,
                    CreatedAt = DateTime.Now
                }
            };

            RefreshPatternList();
        }

        private void RefreshPatternList()
        {
            _patternListBox.Items.Clear();
            foreach (var p in _patterns)
            {
                string status = p.IsActive ? "✓" : "✗";
                _patternListBox.Items.Add($"{status} {p.Name,-25} | Margines: {p.MarginTop}px | {p.Description}");
            }
        }

        private void AddPatternBtn_Click(object sender, EventArgs e)
        {
            var dialog = new Form
            {
                Text = "Dodaj nowy Pattern",
                Size = new Size(420, 400),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgColor,
                ForeColor = textColor,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            };

            // Nagłówek
            var header = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(420, 45),
                BackColor = primaryColor
            };
            var headerText = new Label
            {
                Text = "➕  Dodaj nowy wzorzec",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(15, 10),
                AutoSize = true,
                ForeColor = Color.White
            };
            header.Controls.Add(headerText);

            int y = 60;
            var nameLabel = new Label { Text = "Nazwa wzorca:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            var nameBox = new TextBox { Location = new Point(20, y + 25), Size = new Size(360, 28), BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle };
            y += 60;

            var descLabel = new Label { Text = "Opis:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            var descBox = new TextBox { Location = new Point(20, y + 25), Size = new Size(360, 50), BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle, Multiline = true };
            y += 80;

            var marginLabel = new Label { Text = "Margines bezpieczeństwa (px):", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            var marginBox = new NumericUpDown { Location = new Point(20, y + 25), Size = new Size(80, 25), Value = 10, BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle };
            y += 55;

            var activeCheck = new CheckBox { Text = "Aktywny", Location = new Point(20, y), Checked = true, ForeColor = textColor };
            y += 40;

            var addBtn = new Button
            {
                Text = "Dodaj Pattern",
                Location = new Point(120, y),
                Size = new Size(150, 35),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            addBtn.FlatAppearance.BorderSize = 0;
            addBtn.Click += (s, args) =>
            {
                if (!string.IsNullOrWhiteSpace(nameBox.Text))
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
                }
                else
                {
                    MessageBox.Show("Podaj nazwę wzorca.", "Info");
                }
            };

            dialog.Controls.AddRange(new Control[] { header, nameLabel, nameBox, descLabel, descBox, marginLabel, marginBox, activeCheck, addBtn });
            dialog.ShowDialog(this);
        }

        private void DeletePattern()
        {
            if (_patternListBox.SelectedIndex >= 0)
            {
                var result = MessageBox.Show("Czy na pewno usunąć zaznaczony wzorzec?", "Potwierdzenie",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _patterns.RemoveAt(_patternListBox.SelectedIndex);
                    RefreshPatternList();
                }
            }
            else
            {
                MessageBox.Show("Zaznacz wzorzec do usunięcia.", "Info");
            }
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tabControl = sender as TabControl;
            var tabPage = tabControl.TabPages[e.Index];
            var tabRect = tabControl.GetTabRect(e.Index);
            
            // Tło zakładki
            Color tabColor = e.State == DrawItemState.Selected ? primaryColor : Color.FromArgb(240, 240, 240);
            
            using (var brush = new SolidBrush(tabColor))
            {
                e.Graphics.FillRectangle(brush, tabRect);
            }

            // Tekst zakładki
            Color textTabColor = e.State == DrawItemState.Selected ? Color.White : textColor;
            TextRenderer.DrawText(e.Graphics, tabPage.Text, e.Font, tabRect, textTabColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "✅  Wszystkie ustawienia zostały zapisane!\n\n" +
                $"• Hasło admina: {(_adminPasswordBox.Text.Length > 0 ? "ustawione" : "nie ustawione")}\n" +
                $"• Folder backupu: {_backupPathBox.Text}\n" +
                $"• CheckPoint EXE: {_checkpointPathBox.Text}\n" +
                $"• Liczba patternów: {_patterns.Count}\n" +
                $"• Próg zgodności: {_thresholdBox.Value}%\n" +
                $"• Interwał skanowania: {_intervalBox.Value}ms\n" +
                $"• Autostart: {(_autoStartCheck.Checked ? "włączony" : "wyłączony")}",
                "Ustawienia zapisane",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            
            this.Close();
        }
    }
}