using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;

namespace SecureDesktop.Forms
{
    public class ConfigurationForm : Form
    {
        private readonly Color primaryColor = Color.FromArgb(45, 165, 90);
        private readonly Color bgColor = Color.White;
        private readonly Color textColor = Color.FromArgb(30, 30, 30);
        private readonly Color subtitleColor = Color.FromArgb(100, 100, 100);
        private readonly Color inputBg = Color.FromArgb(245, 245, 245);
        private readonly Color borderColor = Color.FromArgb(220, 220, 220);

        private TextBox _adminPasswordBox;
        private TextBox _backupPathBox;
        private TextBox _checkpointPathBox;
        private TextBox _checkpointArgsBox;
        private TextBox _monitorPathBox;
        private ListBox _patternListBox;
        private NumericUpDown _thresholdBox;
        private NumericUpDown _intervalBox;
        private NumericUpDown _marginBox;
        private CheckBox _autoStartCheck;
        private CheckBox _trayCheck;
        private PictureBox _patternPreviewBox;
        private List<Pattern> _patterns;
        private DatabaseInitializer _db;
        private UserRepository _userRepo;

        private ListView _userListView;

        public ConfigurationForm()
        {
            _patterns = new List<Pattern>();
            this.Icon = Program.AppIcon;
            InitializeDatabase();
            InitializeComponent();
            LoadPatternsFromDatabase();
            LoadSettingsIntoControls();
        }

        private void InitializeDatabase()
        {
            try
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                _db = new DatabaseInitializer(dbPath);
                _db.Initialize();
                _userRepo = new UserRepository(_db);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Blad inicjalizacji bazy: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Konfiguracja SecureDesktop";
            this.Size = new Size(820, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9);
            this.KeyPreview = true;

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };

            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(820, 55),
                BackColor = primaryColor
            };

            var headerTitle = new Label
            {
                Text = "Konfiguracja SecureDesktop",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(20, 12),
                AutoSize = true,
                ForeColor = Color.White
            };

            headerPanel.Controls.Add(headerTitle);

            var tabControl = new TabControl
            {
                Location = new Point(10, 65),
                Size = new Size(785, 490),
                Appearance = TabAppearance.FlatButtons
            };

            var tabGeneral = new TabPage("  Ogolne  ") { BackColor = bgColor };
            var tabPatterns = new TabPage("  Patterny  ") { BackColor = bgColor };
            var tabCheckpoint = new TabPage("  CheckPoint  ") { BackColor = bgColor };
            var tabBackup = new TabPage("  Backup  ") { BackColor = bgColor };
            var tabUsers = new TabPage("  Uzytkownicy  ") { BackColor = bgColor };

            tabControl.TabPages.Add(tabGeneral);
            tabControl.TabPages.Add(tabPatterns);
            tabControl.TabPages.Add(tabCheckpoint);
            tabControl.TabPages.Add(tabBackup);
            tabControl.TabPages.Add(tabUsers);

            BuildGeneralTab(tabGeneral);
            BuildPatternsTab(tabPatterns);
            BuildCheckpointTab(tabCheckpoint);
            BuildBackupTab(tabBackup);
            BuildUsersTab(tabUsers);

            var saveBtn = new Button
            {
                Text = "Zapisz wszystkie ustawienia",
                Location = new Point(250, 565),
                Size = new Size(220, 38),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += SaveAllSettings;

            var cancelBtn = new Button
            {
                Text = "Zamknij",
                Location = new Point(490, 565),
                Size = new Size(100, 38),
                BackColor = Color.White,
                ForeColor = subtitleColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            cancelBtn.FlatAppearance.BorderColor = borderColor;
            cancelBtn.FlatAppearance.BorderSize = 1;
            cancelBtn.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { headerPanel, tabControl, saveBtn, cancelBtn });
        }

        private void LoadSettingsIntoControls()
        {
            try
            {
                if (_db == null) return;
                var data = _db.GetData();
                if (data == null || data.Settings == null) return;

                if (data.Settings.ContainsKey("AdminPassword") && _adminPasswordBox != null)
                    _adminPasswordBox.Text = data.Settings["AdminPassword"];
                if (data.Settings.ContainsKey("AutoStart") && _autoStartCheck != null)
                    _autoStartCheck.Checked = data.Settings["AutoStart"] == "True";
                if (data.Settings.ContainsKey("MinimizeToTray") && _trayCheck != null)
                    _trayCheck.Checked = data.Settings["MinimizeToTray"] == "True";

                if (data.Settings.ContainsKey("BackupPath") && _backupPathBox != null)
                    _backupPathBox.Text = data.Settings["BackupPath"];
                if (data.Settings.ContainsKey("MonitoredFile") && _monitorPathBox != null)
                    _monitorPathBox.Text = data.Settings["MonitoredFile"];

                if (data.Settings.ContainsKey("CheckpointPath") && _checkpointPathBox != null)
                    _checkpointPathBox.Text = data.Settings["CheckpointPath"];
                if (data.Settings.ContainsKey("CheckpointArgs") && _checkpointArgsBox != null)
                    _checkpointArgsBox.Text = data.Settings["CheckpointArgs"];

                if (data.Settings.ContainsKey("PatternThreshold") && _thresholdBox != null)
                {
                    if (int.TryParse(data.Settings["PatternThreshold"], out int th))
                        _thresholdBox.Value = th;
                }
                if (data.Settings.ContainsKey("SearchInterval") && _intervalBox != null)
                {
                    if (int.TryParse(data.Settings["SearchInterval"], out int si))
                        _intervalBox.Value = si;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadSettingsIntoControls error: " + ex.Message);
            }
        }

        private void BuildGeneralTab(TabPage tab)
        {
            int y = 15;

            var passLabel = new Label
            {
                Text = "Haslo administratora:",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };

            _adminPasswordBox = new TextBox
            {
                Location = new Point(220, y - 3),
                Size = new Size(200, 25),
                PasswordChar = '*',
                BackColor = inputBg,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "admin"
            };
            y += 45;

            _autoStartCheck = new CheckBox
            {
                Text = "Uruchamiaj przy starcie Windows",
                Location = new Point(20, y),
                AutoSize = true,
                FlatStyle = FlatStyle.Flat
            };
            y += 30;

            _trayCheck = new CheckBox
            {
                Text = "Minimalizuj do zasobnika",
                Location = new Point(20, y),
                AutoSize = true,
                Checked = true,
                FlatStyle = FlatStyle.Flat
            };

            tab.Controls.AddRange(new Control[] { passLabel, _adminPasswordBox, _autoStartCheck, _trayCheck });
        }

        private void BuildPatternsTab(TabPage tab)
        {
            var listLabel = new Label
            {
                Text = "Lista wzorcow:",
                Location = new Point(15, 15),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                ForeColor = primaryColor
            };

            _patternListBox = new ListBox
            {
                Location = new Point(15, 45),
                Size = new Size(350, 280),
                BackColor = inputBg,
                ForeColor = textColor,
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle
            };

            var addBtn = new Button
            {
                Text = "Zaznacz Pattern z ekranu",
                Location = new Point(15, 340),
                Size = new Size(170, 35),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            addBtn.FlatAppearance.BorderSize = 0;
            addBtn.Click += AddPatternFromScreen;

            var deleteBtn = new Button
            {
                Text = "Usun zaznaczony",
                Location = new Point(195, 340),
                Size = new Size(170, 35),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(220, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9)
            };
            deleteBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 80, 80);
            deleteBtn.FlatAppearance.BorderSize = 1;
            deleteBtn.Click += DeleteSelectedPattern;

            var previewLabel = new Label
            {
                Text = "Podglad:",
                Location = new Point(390, 15),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                ForeColor = primaryColor
            };

            _patternPreviewBox = new PictureBox
            {
                Location = new Point(390, 45),
                Size = new Size(350, 200),
                BackColor = inputBg,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom
            };

            _patternListBox.SelectedIndexChanged += (s, e) =>
            {
                if (_patternListBox.SelectedIndex >= 0 && _patternListBox.SelectedIndex < _patterns.Count)
                {
                    var pattern = _patterns[_patternListBox.SelectedIndex];
                    if (pattern.ImageData != null && pattern.ImageData.Length > 0)
                    {
                        try
                        {
                            using (var ms = new MemoryStream(pattern.ImageData))
                            {
                                _patternPreviewBox.Image = Image.FromStream(ms);
                            }
                        }
                        catch { _patternPreviewBox.Image = null; }
                    }
                    else { _patternPreviewBox.Image = null; }
                    _marginBox.Value = pattern.MarginTop;
                }
            };

            var settingsLabel = new Label
            {
                Text = "Ustawienia wykrywania:",
                Location = new Point(390, 260),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                ForeColor = primaryColor
            };

            var thresholdLabel = new Label { Text = "Prog zgodnosci (%):", Location = new Point(390, 295), AutoSize = true };
            _thresholdBox = new NumericUpDown
            {
                Location = new Point(530, 292), Size = new Size(70, 25),
                Minimum = 50, Maximum = 100, Value = 75,
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle
            };

            var intervalLabel = new Label { Text = "Interwal (ms):", Location = new Point(390, 325), AutoSize = true };
            _intervalBox = new NumericUpDown
            {
                Location = new Point(530, 322), Size = new Size(70, 25),
                Minimum = 100, Maximum = 5000, Value = 200, Increment = 100,
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle
            };

            var marginLabel = new Label { Text = "Margines (px):", Location = new Point(390, 355), AutoSize = true };
            _marginBox = new NumericUpDown
            {
                Location = new Point(530, 352), Size = new Size(70, 25),
                Minimum = 0, Maximum = 100, Value = 10,
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle
            };

            var applySettingsBtn = new Button
            {
                Text = "Zastosuj do zaznaczonego",
                Location = new Point(390, 390), Size = new Size(210, 30),
                BackColor = primaryColor, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            applySettingsBtn.FlatAppearance.BorderSize = 0;
            applySettingsBtn.Click += (s, e) =>
            {
                if (_patternListBox.SelectedIndex >= 0)
                {
                    var p = _patterns[_patternListBox.SelectedIndex];
                    p.MarginTop = (int)_marginBox.Value;
                    p.MarginBottom = (int)_marginBox.Value;
                    p.MarginLeft = (int)_marginBox.Value;
                    p.MarginRight = (int)_marginBox.Value;
                    p.UpdatedAt = DateTime.Now;
                    RefreshPatternList();
                    SavePatternsToDatabase();
                    MessageBox.Show("Zastosowano!", "OK");
                }
                else { MessageBox.Show("Zaznacz wzorzec na liscie.", "Info"); }
            };

            tab.Controls.AddRange(new Control[] { listLabel, _patternListBox, addBtn, deleteBtn,
                                                  previewLabel, _patternPreviewBox, settingsLabel,
                                                  thresholdLabel, _thresholdBox, intervalLabel, _intervalBox,
                                                  marginLabel, _marginBox, applySettingsBtn });
        }

        private void BuildCheckpointTab(TabPage tab)
        {
            int y = 20;

            var pathLabel = new Label { Text = "Sciezka do pliku EXE:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            y += 25;

            _checkpointPathBox = new TextBox
            {
                Location = new Point(20, y), Size = new Size(450, 25),
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle, Text = "notepad.exe"
            };
            var pathBtn = new Button
            {
                Text = "Przegladaj", Location = new Point(480, y), Size = new Size(90, 25),
                BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            pathBtn.FlatAppearance.BorderSize = 0;
            pathBtn.Click += (s, ev) => { using (var dlg = new OpenFileDialog()) { dlg.Filter = "EXE|*.exe"; if (dlg.ShowDialog() == DialogResult.OK) _checkpointPathBox.Text = dlg.FileName; } };
            y += 40;

            var argsLabel = new Label { Text = "Parametry uruchomienia:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            y += 25;
            _checkpointArgsBox = new TextBox { Location = new Point(20, y), Size = new Size(300, 25), BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle };
            y += 45;

            var testBtn = new Button
            {
                Text = "Testuj uruchomienie",
                Location = new Point(150, y), Size = new Size(200, 40),
                BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            testBtn.FlatAppearance.BorderSize = 0;
            testBtn.Click += (s, ev) =>
            {
                try { System.Diagnostics.Process.Start(_checkpointPathBox.Text, _checkpointArgsBox.Text); }
                catch (Exception ex) { MessageBox.Show("Blad: " + ex.Message); }
            };

            tab.Controls.AddRange(new Control[] { pathLabel, _checkpointPathBox, pathBtn, argsLabel, _checkpointArgsBox, testBtn });
        }

        private void BuildBackupTab(TabPage tab)
        {
            int y = 20;

            var backupLabel = new Label
            {
                Text = "Folder docelowy backupu:",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };
            y += 25;

            _backupPathBox = new TextBox
            {
                Location = new Point(20, y),
                Size = new Size(350, 25),
                BackColor = inputBg,
                BorderStyle = BorderStyle.FixedSingle,
                Text = ".\\Backup"
            };
            var backupBrowseBtn = new Button
            {
                Text = "Przegladaj",
                Location = new Point(380, y),
                Size = new Size(90, 25),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            backupBrowseBtn.FlatAppearance.BorderSize = 0;
            backupBrowseBtn.Click += (s, ev) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                        _backupPathBox.Text = dlg.SelectedPath;
                }
            };
            y += 45;

            var monitorLabel = new Label
            {
                Text = "Plik do monitorowania (backup przy logowaniu):",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };
            y += 25;

            _monitorPathBox = new TextBox
            {
                Location = new Point(20, y),
                Size = new Size(350, 25),
                BackColor = inputBg,
                BorderStyle = BorderStyle.FixedSingle,
                Text = ""
            };
            var monitorBrowseBtn = new Button
            {
                Text = "Przegladaj",
                Location = new Point(380, y),
                Size = new Size(90, 25),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            monitorBrowseBtn.FlatAppearance.BorderSize = 0;
            monitorBrowseBtn.Click += (s, ev) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Wszystkie pliki|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                        _monitorPathBox.Text = dlg.FileName;
                }
            };
            y += 55;

            var backupNowBtn = new Button
            {
                Text = "Wykonaj backup teraz",
                Location = new Point(100, y),
                Size = new Size(250, 40),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            backupNowBtn.FlatAppearance.BorderSize = 0;
            backupNowBtn.Click += (s, ev) =>
            {
                string sourcePath = _monitorPathBox.Text;
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    MessageBox.Show("Wybierz plik do backupu.", "Info");
                    return;
                }

                try
                {
                    var backupService = new Services.BackupService();
                    string result = backupService.CreateBackup(sourcePath, _backupPathBox.Text);
                    MessageBox.Show("Backup utworzony!\n\n" + result, "Sukces");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Blad: " + ex.Message, "Blad");
                }
            };

            tab.Controls.AddRange(new Control[]
            {
                backupLabel, _backupPathBox, backupBrowseBtn,
                monitorLabel, _monitorPathBox, monitorBrowseBtn,
                backupNowBtn
            });
        }

        private void BuildUsersTab(TabPage tab)
        {
            var titleLabel = new Label
            {
                Text = "Zarzadzanie uzytkownikami",
                Location = new Point(15, 10),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                ForeColor = primaryColor
            };

            _userListView = new ListView
            {
                Location = new Point(15, 40),
                Size = new Size(550, 320),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            _userListView.Columns.Add("ID", 50);
            _userListView.Columns.Add("Numer identyfikacyjny", 200);
            _userListView.Columns.Add("Rola", 100);
            _userListView.Columns.Add("Aktywny", 80);

            RefreshUserList();

            var addBtn = new Button
            {
                Text = "Dodaj uzytkownika",
                Location = new Point(580, 40),
                Size = new Size(150, 35),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            addBtn.FlatAppearance.BorderSize = 0;
            addBtn.Click += AddUser;

            var deleteBtn = new Button
            {
                Text = "Usun (dezaktywuj)",
                Location = new Point(580, 85),
                Size = new Size(150, 35),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(220, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            deleteBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 80, 80);
            deleteBtn.FlatAppearance.BorderSize = 1;
            deleteBtn.Click += DeleteUser;

            var toggleAdminBtn = new Button
            {
                Text = "Zmien role (Admin/User)",
                Location = new Point(580, 130),
                Size = new Size(150, 35),
                BackColor = Color.White,
                ForeColor = primaryColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            toggleAdminBtn.FlatAppearance.BorderColor = primaryColor;
            toggleAdminBtn.FlatAppearance.BorderSize = 1;
            toggleAdminBtn.Click += ToggleUserRole;

            tab.Controls.AddRange(new Control[] { titleLabel, _userListView, addBtn, deleteBtn, toggleAdminBtn });
        }

        private void RefreshUserList()
        {
            if (_userListView == null || _userRepo == null) return;
            _userListView.Items.Clear();
            var users = _userRepo.GetAllUsers();
            foreach (var u in users)
            {
                var item = new ListViewItem(u.Id.ToString());
                item.SubItems.Add(u.IdentificationNumber);
                item.SubItems.Add(u.IsAdmin ? "Administrator" : "Uzytkownik");
                item.SubItems.Add(u.IsActive ? "Tak" : "Nie");
                _userListView.Items.Add(item);
            }
        }

        private void AddUser(object sender, EventArgs e)
        {
            var dialog = new Form
            {
                Text = "Dodaj uzytkownika",
                Size = new Size(350, 250),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgColor,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false
            };

            var idLabel = new Label { Text = "Numer identyfikacyjny:", Location = new Point(20, 20), AutoSize = true };
            var idBox = new TextBox { Location = new Point(20, 45), Size = new Size(280, 25), BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle };

            var adminCheck = new CheckBox { Text = "Uprawnienia administratora", Location = new Point(20, 85), AutoSize = true };

            var okBtn = new Button { Text = "Dodaj", Location = new Point(100, 130), Size = new Size(100, 35), BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            okBtn.FlatAppearance.BorderSize = 0;
            okBtn.Click += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(idBox.Text))
                {
                    MessageBox.Show("Wprowadz numer identyfikacyjny.", "Info");
                    return;
                }
                if (_userRepo.GetByIdentificationNumber(idBox.Text) != null)
                {
                    MessageBox.Show("Uzytkownik o takim numerze juz istnieje.", "Blad");
                    return;
                }

                var newUser = new User
                {
                    IdentificationNumber = idBox.Text,
                    IsAdmin = adminCheck.Checked,
                    IsActive = true
                };
                _userRepo.AddUser(newUser);
                RefreshUserList();
                dialog.Close();
            };

            var cancelBtn = new Button { Text = "Anuluj", Location = new Point(210, 130), Size = new Size(100, 35), BackColor = Color.White, FlatStyle = FlatStyle.Flat };
            cancelBtn.Click += (s, args) => dialog.Close();

            dialog.Controls.AddRange(new Control[] { idLabel, idBox, adminCheck, okBtn, cancelBtn });
            dialog.ShowDialog(this);
        }

        private void DeleteUser(object sender, EventArgs e)
        {
            if (_userListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Zaznacz uzytkownika do usuniecia.", "Info");
                return;
            }
            var userId = int.Parse(_userListView.SelectedItems[0].Text);
            var user = _userRepo.GetAllUsers().FirstOrDefault(u => u.Id == userId);
            if (user == null) return;

            if (user.IdentificationNumber == "admin")
            {
                MessageBox.Show("Nie mozna usunac domyslnego administratora.", "Blad");
                return;
            }

            var result = MessageBox.Show($"Czy na pewno dezaktywowac uzytkownika {user.IdentificationNumber}?", "Potwierdzenie",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                _userRepo.DeleteUser(userId);
                RefreshUserList();
            }
        }

        private void ToggleUserRole(object sender, EventArgs e)
        {
            if (_userListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Zaznacz uzytkownika.", "Info");
                return;
            }
            var userId = int.Parse(_userListView.SelectedItems[0].Text);
            var user = _userRepo.GetAllUsers().FirstOrDefault(u => u.Id == userId);
            if (user == null) return;

            if (user.IdentificationNumber == "admin")
            {
                MessageBox.Show("Nie mozna zmienic roli domyslnego administratora.", "Blad");
                return;
            }

            user.IsAdmin = !user.IsAdmin;
            _userRepo.UpdateUser(user);
            RefreshUserList();
            MessageBox.Show($"Rola zmieniona na: {(user.IsAdmin ? "Administrator" : "Uzytkownik")}", "Sukces");
        }

        private void LoadPatternsFromDatabase()
        {
            try
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");

                if (File.Exists(dbPath))
                {
                    var json = File.ReadAllText(dbPath);
                    var data = JsonConvert.DeserializeObject<DatabaseData>(json);

                    if (data != null && data.Patterns != null && data.Patterns.Count > 0)
                    {
                        _patterns = data.Patterns;
                    }
                    else
                    {
                        _patterns = new List<Pattern>
                        {
                            new Pattern
                            {
                                Id = 1,
                                Name = "Przykladowy wzorzec",
                                Description = "Kliknij 'Zaznacz Pattern z ekranu' aby dodac wlasny",
                                IsActive = true,
                                CreatedAt = DateTime.Now,
                                MarginTop = 10, MarginBottom = 10, MarginLeft = 10, MarginRight = 10
                            }
                        };
                        if (_db != null)
                        {
                            _db.GetData().Patterns = _patterns;
                            _db.Save();
                        }
                    }
                }
                else
                {
                    _patterns = new List<Pattern>();
                }
            }
            catch (Exception ex)
            {
                _patterns = new List<Pattern>();
                System.Diagnostics.Debug.WriteLine("Load patterns error: " + ex.Message);
            }

            RefreshPatternList();
        }

        private void SavePatternsToDatabase()
        {
            try
            {
                if (_db != null)
                {
                    _db.GetData().Patterns = _patterns;
                    _db.Save();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Blad zapisu patternow: " + ex.Message, "Blad");
            }
        }

        private void RefreshPatternList()
        {
            if (_patternListBox == null) return;

            _patternListBox.Items.Clear();
            foreach (var p in _patterns)
            {
                string status = p.IsActive ? "✓" : "✗";
                string name = (p.Name ?? "Bez nazwy").PadRight(25);
                string desc = p.Description ?? "";
                _patternListBox.Items.Add(status + " " + name + " | " + desc);
            }
        }

        private void AddPatternFromScreen(object sender, EventArgs e)
        {
            this.Hide();
            System.Threading.Thread.Sleep(300);

            var screenBounds = Screen.PrimaryScreen.Bounds;
            using (var screenshot = new Bitmap(screenBounds.Width, screenBounds.Height))
            {
                using (var g = Graphics.FromImage(screenshot))
                {
                    g.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size);
                }

                using (var selectionForm = new ScreenSelectionForm(screenshot))
                {
                    if (selectionForm.ShowDialog() == DialogResult.OK)
                    {
                        var selectedImage = selectionForm.SelectedImage;
                        if (selectedImage != null)
                        {
                            var nameDialog = new Form
                            {
                                Text = "Nazwa wzorca",
                                Size = new Size(350, 180),
                                StartPosition = FormStartPosition.CenterScreen,
                                BackColor = Color.White,
                                FormBorderStyle = FormBorderStyle.FixedDialog,
                                TopMost = true
                            };

                            var nameLabel = new Label { Text = "Podaj nazwe wzorca:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 10) };
                            var nameBox = new TextBox { Location = new Point(20, 50), Size = new Size(290, 25), Font = new Font("Segoe UI", 10), BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle };
                            var okBtn = new Button { Text = "Zapisz", Location = new Point(100, 90), Size = new Size(120, 35), BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                            okBtn.FlatAppearance.BorderSize = 0;
                            okBtn.Click += (s2, args) =>
                            {
                                if (!string.IsNullOrWhiteSpace(nameBox.Text))
                                {
                                    byte[] imageData;
                                    using (var ms = new MemoryStream())
                                    {
                                        selectedImage.Save(ms, ImageFormat.Png);
                                        imageData = ms.ToArray();
                                    }

                                    var newPattern = new Pattern
                                    {
                                        Id = _patterns.Count > 0 ? _patterns.Max(p => p.Id) + 1 : 1,
                                        Name = nameBox.Text,
                                        Description = "Wzorzec dodany " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                                        ImageData = imageData,
                                        MarginTop = (int)_marginBox.Value,
                                        MarginBottom = (int)_marginBox.Value,
                                        MarginLeft = (int)_marginBox.Value,
                                        MarginRight = (int)_marginBox.Value,
                                        IsActive = true,
                                        CreatedAt = DateTime.Now
                                    };

                                    _patterns.Add(newPattern);
                                    SavePatternsToDatabase();
                                    RefreshPatternList();
                                    nameDialog.Close();
                                }
                                else
                                {
                                    MessageBox.Show("Podaj nazwe wzorca.", "Info");
                                }
                            };

                            nameDialog.Controls.AddRange(new Control[] { nameLabel, nameBox, okBtn });
                            nameDialog.ShowDialog();
                        }
                    }
                }
            }

            this.Show();
        }

        private void DeleteSelectedPattern(object sender, EventArgs e)
        {
            if (_patternListBox.SelectedIndex >= 0)
            {
                var result = MessageBox.Show("Czy na pewno usunac zaznaczony wzorzec?", "Potwierdzenie",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _patterns.RemoveAt(_patternListBox.SelectedIndex);
                    _patternPreviewBox.Image = null;
                    SavePatternsToDatabase();
                    RefreshPatternList();
                }
            }
            else
            {
                MessageBox.Show("Zaznacz wzorzec do usuniecia.", "Info");
            }
        }

        private void SaveAllSettings(object sender, EventArgs e)
        {
            try
            {
                if (_db != null)
                {
                    var data = _db.GetData();

                    data.Patterns = _patterns;

                    data.Settings["AdminPassword"] = _adminPasswordBox.Text;
                    data.Settings["AutoStart"] = _autoStartCheck?.Checked.ToString() ?? "false";
                    data.Settings["MinimizeToTray"] = _trayCheck?.Checked.ToString() ?? "true";

                    data.Settings["BackupPath"] = _backupPathBox.Text;
                    if (_monitorPathBox != null)
                        data.Settings["MonitoredFile"] = _monitorPathBox.Text;

                    data.Settings["CheckpointPath"] = _checkpointPathBox.Text;
                    data.Settings["CheckpointArgs"] = _checkpointArgsBox.Text;

                    data.Settings["PatternThreshold"] = _thresholdBox.Value.ToString();
                    data.Settings["SearchInterval"] = _intervalBox.Value.ToString();

                    _db.Save();

                    MessageBox.Show("Ustawienia zapisane!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Blad: Brak polaczenia z baza danych.", "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Blad zapisu: " + ex.Message, "Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class ScreenSelectionForm : Form
    {
        private Point _startPoint;
        private Point _endPoint;
        private Rectangle _selectedRect;
        private bool _isSelecting;
        private Bitmap _screenshot;
        private Image _resultImage;

        public Image SelectedImage => _resultImage;

        public ScreenSelectionForm(Bitmap screenshot)
        {
            _screenshot = screenshot;

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.Cursor = Cursors.Cross;
            this.DoubleBuffered = true;
            this.BackgroundImage = screenshot;
            this.BackgroundImageLayout = ImageLayout.Stretch;
            this.Opacity = 0.8;

            var infoLabel = new Label
            {
                Text = "Zaznacz obszar. ENTER = zatwierdz, ESC = anuluj",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(180, 0, 0, 0),
                Location = new Point(0, 0),
                Size = new Size(Screen.PrimaryScreen.Bounds.Width, 50),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(infoLabel);

            this.MouseDown += (s, e) => { _startPoint = e.Location; _isSelecting = true; };
            this.MouseMove += (s, e) => { if (_isSelecting) { _endPoint = e.Location; this.Invalidate(); } };
            this.MouseUp += (s, e) => { _isSelecting = false; _endPoint = e.Location; this.Invalidate(); };

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _selectedRect.Width > 10 && _selectedRect.Height > 10)
                {
                    _resultImage = _screenshot.Clone(_selectedRect, _screenshot.PixelFormat);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_startPoint != Point.Empty && _endPoint != Point.Empty)
            {
                int x = Math.Min(_startPoint.X, _endPoint.X);
                int y = Math.Min(_startPoint.Y, _endPoint.Y);
                int w = Math.Abs(_endPoint.X - _startPoint.X);
                int h = Math.Abs(_endPoint.Y - _startPoint.Y);

                _selectedRect = new Rectangle(x, y, w, h);

                using (var brush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, this.Width, y);
                    e.Graphics.FillRectangle(brush, 0, y + h, this.Width, this.Height - y - h);
                    e.Graphics.FillRectangle(brush, 0, y, x, h);
                    e.Graphics.FillRectangle(brush, x + w, y, this.Width - x - w, h);
                }

                using (var pen = new Pen(Color.FromArgb(45, 165, 90), 3))
                {
                    e.Graphics.DrawRectangle(pen, _selectedRect);
                }

                var sizeText = w + " x " + h + " px";
                using (var font = new Font("Segoe UI", 12, FontStyle.Bold))
                {
                    var textSize = e.Graphics.MeasureString(sizeText, font);
                    var textX = x + (w - (int)textSize.Width) / 2;
                    var textY = y - 25;
                    if (textY < 0) textY = y + h + 5;
                    e.Graphics.DrawString(sizeText, font, Brushes.White, textX, textY);
                }
            }
        }
    }
}