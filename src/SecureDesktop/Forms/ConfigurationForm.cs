using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;
using SecureDesktop.Utils;

namespace SecureDesktop.Forms
{
    public class ConfigurationView : UserControl
    {
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
        private readonly DatabaseInitializer _db;
        private readonly UserRepository _userRepo;
        private ListView _userListView;

        public event Action CloseRequested;
        public event Action DataSaved;

        public ConfigurationView(DatabaseInitializer db)
        {
            _db = db;
            _userRepo = new UserRepository(_db);
            _patterns = new List<Pattern>();
            this.BackColor = UiTheme.Bg;
            this.ForeColor = UiTheme.TextPrimary;
            this.Font = UiFonts.Body;
            this.Dock = DockStyle.Fill;

            InitializeComponent();
            LoadPatternsFromDatabase();
            LoadSettingsIntoControls();
        }

        // ============== UI ==============

        private void InitializeComponent()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = UiTheme.Bg
            };

            header.Controls.Add(new Label
            {
                Text = "Konfiguracja",
                Font = UiFonts.H1,
                Location = new Point(0, 8),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary
            });

            header.Controls.Add(new Label
            {
                Text = "Ustawienia aplikacji, wzorce, kopie zapasowe i użytkownicy",
                Font = UiFonts.Body,
                Location = new Point(0, 34),
                AutoSize = true,
                ForeColor = UiTheme.TextMuted
            });

            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal,
                Font = UiFonts.Body,
                Padding = new Point(16, 6)
            };

            var tabGeneral = MakeTab("Ogólne");
            var tabPatterns = MakeTab("Wzorce");
            var tabCheckpoint = MakeTab("CheckPoint");
            var tabBackup = MakeTab("Backup");
            var tabUsers = MakeTab("Użytkownicy");

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

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = UiTheme.Bg,
                Padding = new Padding(0, 14, 0, 14)
            };

            var saveBtn = RoundedButton.Primary("💾   Zapisz wszystkie ustawienia", 280, 44);
            saveBtn.Location = new Point(0, 14);
            saveBtn.Click += SaveAllSettings;

            var cancelBtn = RoundedButton.Ghost("Powrót do panelu", 180, 44);
            cancelBtn.Location = new Point(292, 14);
            cancelBtn.Click += (s, e) => { var h = CloseRequested; if (h != null) h(); };

            bottomPanel.Controls.Add(saveBtn);
            bottomPanel.Controls.Add(cancelBtn);

            this.Controls.Add(tabControl);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(header);
        }

        private TabPage MakeTab(string text)
        {
            return new TabPage(text)
            {
                BackColor = UiTheme.Bg,
                Padding = new Padding(24, 20, 24, 20)
            };
        }

        private Label MakeFieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = UiFonts.CaptionBold,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(x, y),
                AutoSize = true
            };
        }

        private TextBox MakeTextBox(int x, int y, int w, bool password = false)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 32),
                Font = UiFonts.BodyLarge,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                PasswordChar = password ? '●' : '\0'
            };
        }

        private NumericUpDown MakeNumeric(int x, int y, int w, int min, int max, int val)
        {
            return new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(w, 32),
                Font = UiFonts.Body,
                Minimum = min,
                Maximum = max,
                Value = val,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        // ============== OGÓLNE ==============

        private void BuildGeneralTab(TabPage tab)
        {
            int y = 20;

            tab.Controls.Add(MakeFieldLabel("Nowe hasło administratora", 0, y));
            y += 24;
            _adminPasswordBox = MakeTextBox(0, y, 280, password: true);
            tab.Controls.Add(_adminPasswordBox);
            y += 36;

            tab.Controls.Add(new Label
            {
                Text = "Pozostaw puste, aby nie zmieniać hasła.",
                Font = UiFonts.Small,
                ForeColor = UiTheme.TextMuted,
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 36;

            _autoStartCheck = new CheckBox
            {
                Text = "Uruchamiaj przy starcie Windows",
                Font = UiFonts.Body,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(0, y),
                AutoSize = true,
                FlatStyle = FlatStyle.Flat
            };
            tab.Controls.Add(_autoStartCheck);
            y += 30;

            _trayCheck = new CheckBox
            {
                Text = "Minimalizuj do zasobnika systemowego",
                Font = UiFonts.Body,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(0, y),
                AutoSize = true,
                Checked = true,
                FlatStyle = FlatStyle.Flat
            };
            tab.Controls.Add(_trayCheck);
        }

        // ============== WZORCE ==============

        private void BuildPatternsTab(TabPage tab)
        {
            tab.Controls.Add(new Label
            {
                Text = "Lista wzorców",
                Font = UiFonts.H3,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(0, 0),
                AutoSize = true
            });

            _patternListBox = new ListBox
            {
                Location = new Point(0, 28),
                Size = new Size(380, 260),
                Font = UiFonts.MonoSmall,
                BorderStyle = BorderStyle.None,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary
            };
            tab.Controls.Add(_patternListBox);

            var addBtn = RoundedButton.SoftGreen("➕   Zaznacz z ekranu", 184, 40);
            addBtn.Location = new Point(0, 298);
            addBtn.Click += AddPatternFromScreen;
            tab.Controls.Add(addBtn);

            var deleteBtn = RoundedButton.SoftRed("🗑   Usuń zaznaczony", 184, 40);
            deleteBtn.Location = new Point(196, 298);
            deleteBtn.Click += DeleteSelectedPattern;
            tab.Controls.Add(deleteBtn);

            var testBtn = RoundedButton.SoftBlue("🔍   Testuj wzorzec (na ekranie)", 380, 42);
            testBtn.Location = new Point(0, 348);
            testBtn.Click += TestSelectedPattern;
            tab.Controls.Add(testBtn);

            tab.Controls.Add(new Label
            {
                Text = "Podgląd",
                Font = UiFonts.H3,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(420, 0),
                AutoSize = true
            });

            _patternPreviewBox = new PictureBox
            {
                Location = new Point(420, 28),
                Size = new Size(360, 220),
                BackColor = UiTheme.SurfaceAlt,
                BorderStyle = BorderStyle.None,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            tab.Controls.Add(_patternPreviewBox);

            _patternListBox.SelectedIndexChanged += (s, e) =>
            {
                if (_patternListBox.SelectedIndex < 0 || _patternListBox.SelectedIndex >= _patterns.Count) return;

                var pattern = _patterns[_patternListBox.SelectedIndex];
                if (pattern.ImageData != null && pattern.ImageData.Length > 0)
                {
                    try
                    {
                        using (var ms = new MemoryStream(pattern.ImageData))
                        {
                            if (_patternPreviewBox.Image != null) _patternPreviewBox.Image.Dispose();
                            _patternPreviewBox.Image = Image.FromStream(ms);
                        }
                    }
                    catch { _patternPreviewBox.Image = null; }
                }
                else _patternPreviewBox.Image = null;

                _marginBox.Value = pattern.MarginTop;
                int thresholdPercent = (int)Math.Round(pattern.MatchThreshold * 100.0);
                thresholdPercent = Math.Max((int)_thresholdBox.Minimum, Math.Min((int)_thresholdBox.Maximum, thresholdPercent));
                _thresholdBox.Value = thresholdPercent;
            };

            tab.Controls.Add(new Label
            {
                Text = "Parametry rozpoznawania",
                Font = UiFonts.H3,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(420, 268),
                AutoSize = true
            });

            tab.Controls.Add(MakeFieldLabel("Próg NCC (%)", 420, 300));
            _thresholdBox = MakeNumeric(560, 296, 70, 50, 100, 85);
            tab.Controls.Add(_thresholdBox);

            tab.Controls.Add(MakeFieldLabel("Interwał (ms)", 420, 334));
            _intervalBox = MakeNumeric(560, 330, 70, 100, 5000, 200);
            _intervalBox.Increment = 100;
            tab.Controls.Add(_intervalBox);

            tab.Controls.Add(MakeFieldLabel("Margines (px)", 420, 368));
            _marginBox = MakeNumeric(560, 364, 70, 0, 100, 10);
            tab.Controls.Add(_marginBox);

            var applySettingsBtn = RoundedButton.Primary("Zastosuj do zaznaczonego", 210, 38);
            applySettingsBtn.Location = new Point(420, 406);
            applySettingsBtn.Click += (s, e) =>
            {
                if (_patternListBox.SelectedIndex >= 0)
                {
                    var p = _patterns[_patternListBox.SelectedIndex];
                    p.MarginTop = (int)_marginBox.Value;
                    p.MarginBottom = (int)_marginBox.Value;
                    p.MarginLeft = (int)_marginBox.Value;
                    p.MarginRight = (int)_marginBox.Value;
                    p.MatchThreshold = (double)_thresholdBox.Value / 100.0;
                    p.UpdatedAt = DateTime.Now;
                    RefreshPatternList();
                    SavePatternsToDatabase();
                    MessageBox.Show("Zastosowano!", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else MessageBox.Show("Zaznacz wzorzec na liście.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            tab.Controls.Add(applySettingsBtn);
        }

        private void TestSelectedPattern(object sender, EventArgs e)
        {
            if (_patternListBox.SelectedIndex < 0 || _patternListBox.SelectedIndex >= _patterns.Count)
            {
                MessageBox.Show("Zaznacz wzorzec do przetestowania.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var pattern = _patterns[_patternListBox.SelectedIndex];
            if (pattern.ImageData == null || pattern.ImageData.Length == 0)
            {
                MessageBox.Show("Ten wzorzec nie ma obrazu.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var parentForm = this.FindForm();
            if (parentForm != null)
                parentForm.WindowState = FormWindowState.Minimized;
            System.Threading.Thread.Sleep(400);

            Services.PatternTestResult result = null;
            try
            {
                result = Services.PatternRecognitionService.TestPatternAgainstCurrentScreen(pattern);
            }
            finally
            {
                if (parentForm != null)
                {
                    parentForm.WindowState = FormWindowState.Normal;
                    parentForm.Activate();
                }
            }

            if (result == null || result.Error != null)
            {
                MessageBox.Show("Błąd testu: " + (result == null ? "nieznany" : result.Error), "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            double gap12 = result.Score - result.SecondScore;
            string verdict;
            if (result.Found && gap12 >= 0.15) verdict = "✅ WZORZEC DOBRY";
            else if (result.Found && gap12 >= 0.05) verdict = "⚠️ WZORZEC ŚREDNI";
            else if (result.Found) verdict = "❌ WZORZEC SŁABY (wiele miejsc)";
            else if (result.Score >= 0.6) verdict = "⚠️ NIE widoczny, coś podobnego jest";
            else verdict = "❌ Wzorzec NIE jest widoczny na ekranie";

            string msg =
                "Wzorzec: \"" + pattern.Name + "\"  (" + result.PatternWidth + "x" + result.PatternHeight + " px, kontrast: " + result.StdDev.ToString("F1") + ")\n" +
                "Top-1: " + result.Score.ToString("F3") + "   Top-2: " + result.SecondScore.ToString("F3") + "   Top-3: " + result.ThirdScore.ToString("F3") + "   Próg: " + result.Threshold.ToString("F2") + "\n" +
                verdict;

            using (var preview = new Form
            {
                Text = "Podgląd dopasowania — " + pattern.Name,
                Size = new Size(1100, 850),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = UiTheme.Bg,
                Font = UiFonts.Body
            })
            {
                var stripPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 180,
                    BackColor = UiTheme.SurfaceAlt,
                    Padding = new Padding(16, 8, 16, 8)
                };
                int xpos = 20;
                AddCrop(stripPanel, "WZORZEC", pattern.ImageData, xpos, ref xpos);
                if (result.Crop1 != null) AddCrop(stripPanel, "TOP-1  " + result.Score.ToString("F3"), result.Crop1, xpos, ref xpos);
                if (result.Crop2 != null) AddCrop(stripPanel, "TOP-2  " + result.SecondScore.ToString("F3"), result.Crop2, xpos, ref xpos);
                if (result.Crop3 != null) AddCrop(stripPanel, "TOP-3  " + result.ThirdScore.ToString("F3"), result.Crop3, xpos, ref xpos);

                var pb = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = (Bitmap)result.Screenshot.Clone(),
                    BackColor = UiTheme.Surface
                };
                using (var g = Graphics.FromImage(pb.Image))
                {
                    Color[] colors = { Color.Red, Color.Orange, Color.Gold };
                    int[] widths = { 6, 5, 4 };
                    for (int i = 0; i < result.Candidates.Count && i < 3; i++)
                    {
                        var c = result.Candidates[i];
                        using (var pen = new Pen(colors[i], widths[i]))
                            g.DrawRectangle(pen, new Rectangle(c.X, c.Y, c.Width, c.Height));
                        using (var brush = new SolidBrush(colors[i]))
                        using (var font = new Font("Segoe UI", 14, FontStyle.Bold))
                            g.DrawString("#" + (i + 1) + "  " + c.Score.ToString("F3"), font, brush, c.X, Math.Max(0, c.Y - 26));
                    }
                }

                var info = new Label
                {
                    Text = msg,
                    Dock = DockStyle.Bottom,
                    Height = 76,
                    Padding = new Padding(20, 12, 20, 12),
                    Font = UiFonts.Body,
                    BackColor = UiTheme.Surface,
                    ForeColor = UiTheme.TextPrimary
                };

                preview.Controls.Add(pb);
                preview.Controls.Add(info);
                preview.Controls.Add(stripPanel);
                preview.FormClosed += (s, a) =>
                {
                    if (pb.Image != null) pb.Image.Dispose();
                    if (result.Crop1 != null) result.Crop1.Dispose();
                    if (result.Crop2 != null) result.Crop2.Dispose();
                    if (result.Crop3 != null) result.Crop3.Dispose();
                };
                preview.ShowDialog(this);
            }

            if (result.Screenshot != null) result.Screenshot.Dispose();
        }

        private static void AddCrop(Control parent, string label, byte[] imageData, int xpos, ref int nextX)
        {
            if (imageData == null || imageData.Length == 0) return;
            using (var ms = new MemoryStream(imageData))
            using (var src = new Bitmap(ms))
                AddCrop(parent, label, src, xpos, ref nextX);
        }

        private static void AddCrop(Control parent, string label, Bitmap src, int xpos, ref int nextX)
        {
            const int maxSide = 120;
            int w = src.Width, h = src.Height;
            double scale = Math.Min((double)maxSide / w, (double)maxSide / h);
            if (scale > 1.0 && scale < 2.5) scale = 2.0;
            int drawW = Math.Max(1, (int)(w * scale));
            int drawH = Math.Max(1, (int)(h * scale));

            var pic = new PictureBox
            {
                Location = new Point(xpos, 44),
                Size = new Size(drawW, drawH),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = new Bitmap(src, drawW, drawH),
                BackColor = UiTheme.Surface
            };
            var lbl = new Label
            {
                Text = label,
                Location = new Point(xpos, 20),
                AutoSize = true,
                Font = UiFonts.CaptionBold,
                ForeColor = UiTheme.TextSecondary
            };
            parent.Controls.Add(lbl);
            parent.Controls.Add(pic);
            nextX = xpos + drawW + 24;
        }

        // ============== CHECKPOINT ==============

        private void BuildCheckpointTab(TabPage tab)
        {
            int y = 20;

            tab.Controls.Add(MakeFieldLabel("Ścieżka do pliku EXE", 0, y));
            y += 24;

            _checkpointPathBox = MakeTextBox(0, y, 380);
            _checkpointPathBox.Text = "notepad.exe";
            tab.Controls.Add(_checkpointPathBox);

            var pathBtn = RoundedButton.Ghost("Przeglądaj", 120, 32);
            pathBtn.Location = new Point(392, y);
            pathBtn.Click += (s, ev) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "EXE|*.exe";
                    if (dlg.ShowDialog() == DialogResult.OK) _checkpointPathBox.Text = dlg.FileName;
                }
            };
            tab.Controls.Add(pathBtn);
            y += 44;

            tab.Controls.Add(MakeFieldLabel("Parametry uruchomienia", 0, y));
            y += 24;

            _checkpointArgsBox = MakeTextBox(0, y, 300);
            tab.Controls.Add(_checkpointArgsBox);
            y += 56;

            var testBtn = RoundedButton.Primary("▶   Testuj uruchomienie", 240, 44);
            testBtn.Location = new Point(0, y);
            testBtn.Click += (s, ev) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _checkpointPathBox.Text,
                        Arguments = _checkpointArgsBox.Text ?? "",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            tab.Controls.Add(testBtn);
        }

        // ============== BACKUP ==============

        private void BuildBackupTab(TabPage tab)
        {
            int y = 20;

            tab.Controls.Add(MakeFieldLabel("Folder docelowy backupu", 0, y));
            y += 24;

            _backupPathBox = MakeTextBox(0, y, 380);
            _backupPathBox.Text = ".\\Backup";
            tab.Controls.Add(_backupPathBox);

            var backupBrowseBtn = RoundedButton.Ghost("Przeglądaj", 120, 32);
            backupBrowseBtn.Location = new Point(392, y);
            backupBrowseBtn.Click += (s, ev) =>
            {
                using (var dlg = new FolderBrowserDialog())
                    if (dlg.ShowDialog() == DialogResult.OK) _backupPathBox.Text = dlg.SelectedPath;
            };
            tab.Controls.Add(backupBrowseBtn);
            y += 44;

            tab.Controls.Add(MakeFieldLabel("Plik do monitorowania (backup przy logowaniu)", 0, y));
            y += 24;

            _monitorPathBox = MakeTextBox(0, y, 380);
            tab.Controls.Add(_monitorPathBox);

            var monitorBrowseBtn = RoundedButton.Ghost("Przeglądaj", 120, 32);
            monitorBrowseBtn.Location = new Point(392, y);
            monitorBrowseBtn.Click += (s, ev) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Wszystkie pliki|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        _monitorPathBox.Text = dlg.FileName;
                        try { new Services.FileMonitorService(_db).ResetBaseline(); } catch { }
                    }
                }
            };
            tab.Controls.Add(monitorBrowseBtn);
            y += 56;

            var backupNowBtn = RoundedButton.Primary("💾   Wykonaj backup teraz", 260, 44);
            backupNowBtn.Location = new Point(0, y);
            backupNowBtn.Click += (s, ev) =>
            {
                string sourcePath = _monitorPathBox.Text;
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    MessageBox.Show("Wybierz plik do backupu.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    var backupService = new Services.BackupService();
                    string result = backupService.CreateBackup(sourcePath, _backupPathBox.Text);
                    new Services.FileMonitorService(_db).SaveBaseline(sourcePath);
                    MessageBox.Show("Backup utworzony!\n\n" + result, "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            tab.Controls.Add(backupNowBtn);
        }

        // ============== UŻYTKOWNICY ==============

        private void BuildUsersTab(TabPage tab)
        {
            tab.Controls.Add(new Label
            {
                Text = "Zarządzanie użytkownikami",
                Font = UiFonts.H3,
                Location = new Point(0, 0),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary
            });

            _userListView = new ListView
            {
                Location = new Point(0, 28),
                Size = new Size(560, 360),
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiFonts.Body
            };
            _userListView.Columns.Add("ID", 60);
            _userListView.Columns.Add("Numer identyfikacyjny", 240);
            _userListView.Columns.Add("Rola", 130);
            _userListView.Columns.Add("Aktywny", 90);
            RefreshUserList();
            tab.Controls.Add(_userListView);

            var addBtn = RoundedButton.Primary("➕   Dodaj", 180, 40);
            addBtn.Location = new Point(580, 28);
            addBtn.Click += AddUser;
            tab.Controls.Add(addBtn);

            var deleteBtn = RoundedButton.SoftRed("🗑   Dezaktywuj", 180, 40);
            deleteBtn.Location = new Point(580, 76);
            deleteBtn.Click += DeleteUser;
            tab.Controls.Add(deleteBtn);

            var toggleAdminBtn = RoundedButton.Ghost("↺   Zmień rolę", 180, 40);
            toggleAdminBtn.Location = new Point(580, 124);
            toggleAdminBtn.Click += ToggleUserRole;
            tab.Controls.Add(toggleAdminBtn);
        }

        private void RefreshUserList()
        {
            if (_userListView == null || _userRepo == null) return;
            _userListView.Items.Clear();
            foreach (var u in _userRepo.GetAllUsers())
            {
                var item = new ListViewItem(u.Id.ToString());
                item.SubItems.Add(u.IdentificationNumber);
                item.SubItems.Add(u.IsAdmin ? "Administrator" : "Użytkownik");
                item.SubItems.Add(u.IsActive ? "Tak" : "Nie");
                _userListView.Items.Add(item);
            }
        }

        private void AddUser(object sender, EventArgs e)
        {
            using (var dialog = new Form
            {
                Text = "Dodaj użytkownika",
                Size = new Size(400, 340),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = UiTheme.Bg,
                Font = UiFonts.Body,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                var idLabel = MakeFieldLabel("Numer identyfikacyjny", 24, 24);
                var idBox = MakeTextBox(24, 48, 336);
                var passLabel = MakeFieldLabel("Hasło", 24, 92);
                var passBox = MakeTextBox(24, 116, 336, password: true);
                var adminCheck = new CheckBox
                {
                    Text = "Uprawnienia administratora",
                    Font = UiFonts.Body,
                    Location = new Point(24, 168),
                    AutoSize = true,
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = UiTheme.TextPrimary
                };

                var okBtn = RoundedButton.Primary("Dodaj", 120, 42);
                okBtn.Location = new Point(120, 216);
                okBtn.Click += (s, args) =>
                {
                    if (string.IsNullOrWhiteSpace(idBox.Text)) { MessageBox.Show("Wprowadź numer identyfikacyjny.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    if (string.IsNullOrWhiteSpace(passBox.Text)) { MessageBox.Show("Wprowadź hasło.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    if (_userRepo.GetByIdentificationNumber(idBox.Text) != null)
                    {
                        MessageBox.Show("Użytkownik o takim numerze już istnieje.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    var salt = SecurityHelper.GenerateSalt();
                    _userRepo.AddUser(new User
                    {
                        IdentificationNumber = idBox.Text.Trim(),
                        Salt = salt,
                        PasswordHash = SecurityHelper.HashPassword(passBox.Text, salt),
                        IsAdmin = adminCheck.Checked,
                        IsActive = true
                    });
                    RefreshUserList();
                    dialog.Close();
                };

                var cancelBtn = RoundedButton.Ghost("Anuluj", 108, 42);
                cancelBtn.Location = new Point(252, 216);
                cancelBtn.Click += (s, args) => dialog.Close();

                dialog.Controls.AddRange(new Control[] { idLabel, idBox, passLabel, passBox, adminCheck, okBtn, cancelBtn });
                dialog.ShowDialog(this);
            }
        }

        private void DeleteUser(object sender, EventArgs e)
        {
            if (_userListView.SelectedItems.Count == 0) { MessageBox.Show("Zaznacz użytkownika do usunięcia.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var userId = int.Parse(_userListView.SelectedItems[0].Text);
            var user = _userRepo.GetAllUsers().FirstOrDefault(u => u.Id == userId);
            if (user == null) return;
            if (user.IdentificationNumber == "admin") { MessageBox.Show("Nie można usunąć domyślnego administratora.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            if (MessageBox.Show("Czy na pewno dezaktywować użytkownika " + user.IdentificationNumber + "?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _userRepo.DeleteUser(userId);
                RefreshUserList();
            }
        }

        private void ToggleUserRole(object sender, EventArgs e)
        {
            if (_userListView.SelectedItems.Count == 0) { MessageBox.Show("Zaznacz użytkownika.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var userId = int.Parse(_userListView.SelectedItems[0].Text);
            var user = _userRepo.GetAllUsers().FirstOrDefault(u => u.Id == userId);
            if (user == null) return;
            if (user.IdentificationNumber == "admin") { MessageBox.Show("Nie można zmienić roli domyślnego administratora.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            user.IsAdmin = !user.IsAdmin;
            _userRepo.UpdateUser(user);
            RefreshUserList();
            MessageBox.Show("Rola zmieniona na: " + (user.IsAdmin ? "Administrator" : "Użytkownik"), "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ============== DANE ==============

        private void LoadSettingsIntoControls()
        {
            try
            {
                if (_db == null) return;
                var data = _db.GetData();
                if (data == null || data.Settings == null) return;

                bool b;
                if (data.Settings.TryGetValue("AutoStart", out var autostart) && _autoStartCheck != null)
                    _autoStartCheck.Checked = bool.TryParse(autostart, out b) && b;
                if (data.Settings.TryGetValue("MinimizeToTray", out var tray) && _trayCheck != null)
                    _trayCheck.Checked = bool.TryParse(tray, out b) && b;

                string s;
                if (data.Settings.TryGetValue("BackupPath", out s) && _backupPathBox != null)
                    _backupPathBox.Text = s;
                if (data.Settings.TryGetValue("MonitoredFile", out s) && _monitorPathBox != null)
                    _monitorPathBox.Text = s;
                if (data.Settings.TryGetValue("CheckpointPath", out s) && _checkpointPathBox != null)
                    _checkpointPathBox.Text = s;
                if (data.Settings.TryGetValue("CheckpointArgs", out s) && _checkpointArgsBox != null)
                    _checkpointArgsBox.Text = s;

                int iv;
                if (data.Settings.TryGetValue("PatternThreshold", out s) && _thresholdBox != null &&
                    int.TryParse(s, out iv))
                    _thresholdBox.Value = Math.Max(_thresholdBox.Minimum, Math.Min(_thresholdBox.Maximum, iv));

                if (data.Settings.TryGetValue("SearchInterval", out s) && _intervalBox != null &&
                    int.TryParse(s, out iv))
                    _intervalBox.Value = Math.Max(_intervalBox.Minimum, Math.Min(_intervalBox.Maximum, iv));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("LoadSettings: " + ex.Message); }
        }

        private void LoadPatternsFromDatabase()
        {
            try
            {
                var data = _db.GetData();
                if (data != null && data.Patterns != null && data.Patterns.Count > 0)
                    _patterns = data.Patterns;
                else
                {
                    _patterns = new List<Pattern>
                    {
                        new Pattern
                        {
                            Id = 1, Name = "Przykładowy wzorzec",
                            Description = "Kliknij 'Zaznacz z ekranu' aby dodać własny",
                            IsActive = false, CreatedAt = DateTime.Now,
                            MarginTop = 10, MarginBottom = 10, MarginLeft = 10, MarginRight = 10
                        }
                    };
                    if (_db != null) { _db.GetData().Patterns = _patterns; _db.Save(); }
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
                    if (_patterns.Count > 0)
                        _db.GetData().NextPatternId = _patterns.Max(p => p.Id) + 1;
                    _db.Save();
                }
            }
            catch (Exception ex) { MessageBox.Show("Błąd zapisu wzorców: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RefreshPatternList()
        {
            if (_patternListBox == null) return;
            _patternListBox.Items.Clear();
            foreach (var p in _patterns)
            {
                string status = p.IsActive ? "✓" : "✗";
                string name = (p.Name ?? "Bez nazwy").PadRight(25);
                string threshold = " [" + (int)Math.Round(p.MatchThreshold * 100.0) + "%]";
                _patternListBox.Items.Add(status + "  " + name + threshold);
            }
        }

        private void AddPatternFromScreen(object sender, EventArgs e)
        {
            var parentForm = this.FindForm();
            if (parentForm != null) parentForm.WindowState = FormWindowState.Minimized;
            System.Threading.Thread.Sleep(400);

            try
            {
                var screenBounds = Screen.PrimaryScreen.Bounds;
                using (var screenshot = new Bitmap(screenBounds.Width, screenBounds.Height))
                {
                    using (var g = Graphics.FromImage(screenshot))
                        g.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size);

                    using (var selectionForm = new ScreenSelectionForm(screenshot))
                    {
                        if (selectionForm.ShowDialog() == DialogResult.OK)
                        {
                            var selectedImage = selectionForm.SelectedImage;
                            if (selectedImage != null) PromptAndSavePattern(selectedImage);
                        }
                    }
                }
            }
            finally
            {
                if (parentForm != null)
                {
                    parentForm.WindowState = FormWindowState.Normal;
                    parentForm.Activate();
                }
            }
        }

        private void PromptAndSavePattern(Image selectedImage)
        {
            using (var nameDialog = new Form
            {
                Text = "Nazwa wzorca",
                Size = new Size(400, 240),
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = UiTheme.Bg,
                Font = UiFonts.Body,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                TopMost = true,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                var nameLabel = new Label
                {
                    Text = "Podaj nazwę wzorca",
                    Font = UiFonts.CaptionBold,
                    ForeColor = UiTheme.TextSecondary,
                    Location = new Point(24, 24),
                    AutoSize = true
                };
                var nameBox = MakeTextBox(24, 48, 336);
                var okBtn = RoundedButton.Primary("Zapisz", 120, 42);
                okBtn.Location = new Point(120, 108);
                okBtn.Click += (s2, args) =>
                {
                    if (string.IsNullOrWhiteSpace(nameBox.Text))
                    {
                        MessageBox.Show("Podaj nazwę wzorca.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    byte[] imageData;
                    using (var ms = new MemoryStream())
                    {
                        selectedImage.Save(ms, ImageFormat.Png);
                        imageData = ms.ToArray();
                    }

                    _patterns.Add(new Pattern
                    {
                        Id = _patterns.Count > 0 ? _patterns.Max(p => p.Id) + 1 : 1,
                        Name = nameBox.Text.Trim(),
                        Description = "Dodany " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        ImageData = imageData,
                        MarginTop = (int)_marginBox.Value,
                        MarginBottom = (int)_marginBox.Value,
                        MarginLeft = (int)_marginBox.Value,
                        MarginRight = (int)_marginBox.Value,
                        MatchThreshold = (double)_thresholdBox.Value / 100.0,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                    SavePatternsToDatabase();
                    RefreshPatternList();
                    nameDialog.Close();
                };
                nameDialog.Controls.AddRange(new Control[] { nameLabel, nameBox, okBtn });
                nameDialog.ShowDialog();
            }
        }

        private void DeleteSelectedPattern(object sender, EventArgs e)
        {
            if (_patternListBox.SelectedIndex >= 0)
            {
                if (MessageBox.Show("Czy na pewno usunąć zaznaczony wzorzec?", "Potwierdzenie",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _patterns.RemoveAt(_patternListBox.SelectedIndex);
                    if (_patternPreviewBox.Image != null)
                    {
                        _patternPreviewBox.Image.Dispose();
                        _patternPreviewBox.Image = null;
                    }
                    SavePatternsToDatabase();
                    RefreshPatternList();
                }
            }
            else MessageBox.Show("Zaznacz wzorzec do usunięcia.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveAllSettings(object sender, EventArgs e)
        {
            try
            {
                if (_db == null)
                {
                    MessageBox.Show("Brak połączenia z bazą.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var data = _db.GetData();
                data.Patterns = _patterns;
                if (_patterns.Count > 0)
                    data.NextPatternId = _patterns.Max(p => p.Id) + 1;

                data.Settings["AutoStart"] = (_autoStartCheck != null && _autoStartCheck.Checked).ToString();
                data.Settings["MinimizeToTray"] = (_trayCheck == null || _trayCheck.Checked).ToString();
                data.Settings["BackupPath"] = _backupPathBox.Text;
                if (_monitorPathBox != null) data.Settings["MonitoredFile"] = _monitorPathBox.Text;
                data.Settings["CheckpointPath"] = _checkpointPathBox.Text;
                data.Settings["CheckpointArgs"] = _checkpointArgsBox.Text;
                data.Settings["PatternThreshold"] = _thresholdBox.Value.ToString();
                data.Settings["SearchInterval"] = _intervalBox.Value.ToString();

                if (!string.IsNullOrWhiteSpace(_adminPasswordBox.Text))
                {
                    var admin = data.Users.FirstOrDefault(u => u.IdentificationNumber == "admin");
                    if (admin != null)
                    {
                        admin.Salt = SecurityHelper.GenerateSalt();
                        admin.PasswordHash = SecurityHelper.HashPassword(_adminPasswordBox.Text, admin.Salt);
                    }
                    _adminPasswordBox.Text = "";
                }

                _db.Save();
                var saved = DataSaved;
                if (saved != null) saved();
                MessageBox.Show("Ustawienia zapisane!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ============== SCREEN SELECTION FORM ==============

    public class ScreenSelectionForm : Form
    {
        private Point _startPoint;
        private Point _endPoint;
        private Rectangle _selectedRect;
        private bool _isSelecting;
        private readonly Bitmap _screenshot;
        private Image _resultImage;

        public Image SelectedImage { get { return _resultImage; } }

        public ScreenSelectionForm(Bitmap screenshot)
        {
            _screenshot = screenshot;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.TopMost = true;
            this.Cursor = Cursors.Cross;
            this.DoubleBuffered = true;
            this.BackgroundImage = screenshot;
            this.BackgroundImageLayout = ImageLayout.None;
            this.Opacity = 0.95;

            var infoLabel = new Label
            {
                Text = "Zaznacz obszar. ENTER = zatwierdź, ESC = anuluj",
                Font = UiFonts.H2,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(200, 0, 0, 0),
                Location = new Point(0, 0),
                Size = new Size(this.Width, 56),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(infoLabel);

            this.MouseDown += (s, e) => { _startPoint = e.Location; _isSelecting = true; };
            this.MouseMove += (s, e) => { if (_isSelecting) { _endPoint = e.Location; this.Invalidate(); } };
            this.MouseUp += (s, e) => { _isSelecting = false; _endPoint = e.Location; this.Invalidate(); };

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _selectedRect.Width > 4 && _selectedRect.Height > 4)
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

                using (var pen = new Pen(UiTheme.Primary, 3))
                    e.Graphics.DrawRectangle(pen, _selectedRect);

                var sizeText = w + " × " + h + " px";
                var textSize = e.Graphics.MeasureString(sizeText, UiFonts.H3);
                e.Graphics.DrawString(sizeText, UiFonts.H3, Brushes.White,
                    x + (w - (int)textSize.Width) / 2,
                    y < 30 ? y + h + 8 : y - 30);
            }
        }
    }
}
