using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Forms
{
    public class ConfigurationForm : Form
    {
        // Kolorystyka
        private readonly Color primaryColor = Color.FromArgb(45, 165, 90);
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
        private PictureBox _patternPreviewBox;
        
        private List<Pattern> _patterns;
        private byte[] _capturedPatternImage;

        public ConfigurationForm()
        {
            _patterns = new List<Pattern>();
            InitializeComponent();
            LoadSamplePatterns();
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
            this.Font = new Font("Segoe UI", 9);

            // === NAGŁÓWEK ===
            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(820, 55),
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
                Size = new Size(785, 490),
                Appearance = TabAppearance.FlatButtons
            };

            var tabGeneral = new TabPage("  Ogólne  ") { BackColor = bgColor };
            BuildGeneralTab(tabGeneral);

            var tabPatterns = new TabPage("  Patterny  ") { BackColor = bgColor };
            BuildPatternsTab(tabPatterns);

            var tabCheckpoint = new TabPage("  CheckPoint  ") { BackColor = bgColor };
            BuildCheckpointTab(tabCheckpoint);

            var tabBackup = new TabPage("  Backup  ") { BackColor = bgColor };
            BuildBackupTab(tabBackup);

            tabControl.TabPages.Add(tabGeneral);
            tabControl.TabPages.Add(tabPatterns);
            tabControl.TabPages.Add(tabCheckpoint);
            tabControl.TabPages.Add(tabBackup);

            // === PRZYCISKI NA DOLE ===
            var saveBtn = new Button
            {
                Text = "💾  Zapisz wszystkie ustawienia",
                Location = new Point(250, 565),
                Size = new Size(220, 38),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, e) =>
            {
                MessageBox.Show("✅ Ustawienia zapisane!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };

            var cancelBtn = new Button
            {
                Text = "Anuluj",
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

        // ==================== ZAKŁADKA OGÓLNE ====================
        private void BuildGeneralTab(TabPage tab)
        {
            int y = 15;

            var passLabel = new Label
            {
                Text = "Hasło administratora:",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };
            
            _adminPasswordBox = new TextBox
            {
                Location = new Point(220, y - 3),
                Size = new Size(200, 25),
                PasswordChar = '●',
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
            y += 45;

            var monitorLabel = new Label
            {
                Text = "Plik do monitorowania:",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10)
            };
            y += 25;

            var monitorPathBox = new TextBox
            {
                Location = new Point(20, y),
                Size = new Size(450, 25),
                BackColor = inputBg,
                BorderStyle = BorderStyle.FixedSingle
            };
            var monitorBtn = new Button
            {
                Text = "Przeglądaj",
                Location = new Point(480, y),
                Size = new Size(90, 25),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            monitorBtn.FlatAppearance.BorderSize = 0;
            monitorBtn.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                        monitorPathBox.Text = dlg.FileName;
                }
            };

            tab.Controls.AddRange(new Control[] { passLabel, _adminPasswordBox, _autoStartCheck, _trayCheck, 
                                                  monitorLabel, monitorPathBox, monitorBtn });
        }

        // ==================== ZAKŁADKA PATTERNY ====================
        private void BuildPatternsTab(TabPage tab)
        {
            // Lewa strona - lista
            var listLabel = new Label
            {
                Text = "📋  Lista wzorców:",
                Location = new Point(15, 15),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                ForeColor = primaryColor
            };

            _patternListBox = new ListBox
            {
                Location = new Point(15, 45),
                Size = new Size(350, 300),
                BackColor = inputBg,
                ForeColor = textColor,
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Przyciski pod listą
            var addBtn = new Button
            {
                Text = "➕  Zaznacz Pattern z ekranu",
                Location = new Point(15, 355),
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
                Text = "🗑️  Usuń zaznaczony",
                Location = new Point(195, 355),
                Size = new Size(170, 35),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(220, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9)
            };
            deleteBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 80, 80);
            deleteBtn.FlatAppearance.BorderSize = 1;
            deleteBtn.Click += (s, e) =>
            {
                if (_patternListBox.SelectedIndex >= 0)
                {
                    _patterns.RemoveAt(_patternListBox.SelectedIndex);
                    RefreshPatternList();
                    _patternPreviewBox.Image = null;
                }
                else MessageBox.Show("Zaznacz wzorzec do usunięcia.");
            };

            // Prawa strona - podgląd
            var previewLabel = new Label
            {
                Text = "🔍  Podgląd:",
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
    SizeMode = PictureBoxSizeMode.Zoom  // TO MUSI BYĆ!
};

            // Podgląd zmienia się po kliknięciu na liście
            _patternListBox.SelectedIndexChanged += (s, e) =>
            {
                if (_patternListBox.SelectedIndex >= 0 && _patternListBox.SelectedIndex < _patterns.Count)
                {
                    var pattern = _patterns[_patternListBox.SelectedIndex];
                    if (pattern.ImageData != null)
                    {
                        using (var ms = new MemoryStream(pattern.ImageData))
                        {
                            _patternPreviewBox.Image = Image.FromStream(ms);
                        }
                    }
                }
            };

            // Ustawienia po prawej
            var settingsLabel = new Label
            {
                Text = "⚙️  Ustawienia:",
                Location = new Point(390, 260),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                AutoSize = true,
                ForeColor = primaryColor
            };

            var thresholdLabel = new Label { Text = "Próg zgodności (%):", Location = new Point(390, 295), AutoSize = true };
            _thresholdBox = new NumericUpDown
            {
                Location = new Point(530, 292),
                Size = new Size(70, 25),
                Minimum = 50, Maximum = 100, Value = 95,
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle
            };

            var intervalLabel = new Label { Text = "Interwał (ms):", Location = new Point(390, 325), AutoSize = true };
            _intervalBox = new NumericUpDown
            {
                Location = new Point(530, 322),
                Size = new Size(70, 25),
                Minimum = 100, Maximum = 5000, Value = 500, Increment = 100,
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle
            };

            var marginLabel = new Label { Text = "Margines (px):", Location = new Point(390, 355), AutoSize = true };
            _marginBox = new NumericUpDown
            {
                Location = new Point(530, 352),
                Size = new Size(70, 25),
                Minimum = 0, Maximum = 100, Value = 10,
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle
            };

            var applySettingsBtn = new Button
            {
                Text = "Zastosuj do zaznaczonego",
                Location = new Point(390, 390),
                Size = new Size(210, 30),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
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
                    RefreshPatternList();
                    MessageBox.Show("✅ Ustawienia zastosowane!", "OK");
                }
            };

            tab.Controls.AddRange(new Control[] { listLabel, _patternListBox, addBtn, deleteBtn,
                                                  previewLabel, _patternPreviewBox, settingsLabel,
                                                  thresholdLabel, _thresholdBox, intervalLabel, _intervalBox,
                                                  marginLabel, _marginBox, applySettingsBtn });
        }

        // ==================== DODAWANIE PATTERNU Z EKRANU ====================
        private void AddPatternFromScreen(object sender, EventArgs e)
        {
            // Ukryj to okno
            this.Hide();
            System.Threading.Thread.Sleep(300);

            // Przechwyć cały ekran
            var screenBounds = Screen.PrimaryScreen.Bounds;
            using (var screenshot = new Bitmap(screenBounds.Width, screenBounds.Height))
            {
                using (var g = Graphics.FromImage(screenshot))
                {
                    g.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size);
                }

                // Pokaż okno do zaznaczania
                using (var selectionForm = new ScreenSelectionForm(screenshot))
                {
                    if (selectionForm.ShowDialog() == DialogResult.OK)
                    {
                        // Zapisz zaznaczony obszar jako pattern
                        var selectedImage = selectionForm.SelectedImage;
                        if (selectedImage != null)
                        {
                            // Zapytaj o nazwę
                            var nameDialog = new Form
                            {
                                Text = "Nazwa wzorca",
                                Size = new Size(350, 180),
                                StartPosition = FormStartPosition.CenterScreen,
                                BackColor = Color.White,
                                FormBorderStyle = FormBorderStyle.FixedDialog,
                                TopMost = true
                            };

                            var nameLabel = new Label
                            {
                                Text = "Podaj nazwę wzorca:",
                                Location = new Point(20, 20),
                                AutoSize = true,
                                Font = new Font("Segoe UI", 10)
                            };
                            var nameBox = new TextBox
                            {
                                Location = new Point(20, 50),
                                Size = new Size(290, 25),
                                Font = new Font("Segoe UI", 10),
                                BackColor = inputBg,
                                BorderStyle = BorderStyle.FixedSingle
                            };
                            var okBtn = new Button
                            {
                                Text = "Zapisz",
                                Location = new Point(100, 90),
                                Size = new Size(120, 35),
                                BackColor = primaryColor,
                                ForeColor = Color.White,
                                FlatStyle = FlatStyle.Flat
                            };
                            okBtn.FlatAppearance.BorderSize = 0;
                            okBtn.Click += (s2, args) =>
                            {
                                if (!string.IsNullOrWhiteSpace(nameBox.Text))
                                {
                                    // Konwertuj obraz do byte[]
                                    byte[] imageData;
                                    using (var ms = new MemoryStream())
                                    {
                                        selectedImage.Save(ms, ImageFormat.Png);
                                        imageData = ms.ToArray();
                                    }

                                    _patterns.Add(new Pattern
                                    {
                                        Id = _patterns.Count + 1,
                                        Name = nameBox.Text,
                                        Description = $"Wzorzec {_patterns.Count + 1}",
                                        ImageData = imageData,
                                        MarginTop = (int)_marginBox.Value,
                                        MarginBottom = (int)_marginBox.Value,
                                        MarginLeft = (int)_marginBox.Value,
                                        MarginRight = (int)_marginBox.Value,
                                        IsActive = true,
                                        CreatedAt = DateTime.Now
                                    });

                                    RefreshPatternList();
                                    nameDialog.Close();
                                }
                            };

                            nameDialog.Controls.AddRange(new Control[] { nameLabel, nameBox, okBtn });
                            nameDialog.ShowDialog();
                        }
                    }
                }
            }

            // Pokaż ponownie okno konfiguracji
            this.Show();
        }

        // ==================== ZAKŁADKA CHECKPOINT ====================
        private void BuildCheckpointTab(TabPage tab)
        {
            int y = 20;

            var pathLabel = new Label { Text = "Ścieżka do pliku EXE:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            y += 25;

            _checkpointPathBox = new TextBox
            {
                Location = new Point(20, y), Size = new Size(450, 25),
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle, Text = "notepad.exe"
            };
            var pathBtn = new Button
            {
                Text = "Przeglądaj", Location = new Point(480, y), Size = new Size(90, 25),
                BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            pathBtn.FlatAppearance.BorderSize = 0;
            pathBtn.Click += (s, e) => { using (var dlg = new OpenFileDialog()) { dlg.Filter = "EXE|*.exe"; if (dlg.ShowDialog() == DialogResult.OK) _checkpointPathBox.Text = dlg.FileName; } };
            y += 40;

            var argsLabel = new Label { Text = "Parametry:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            y += 25;
            _checkpointArgsBox = new TextBox { Location = new Point(20, y), Size = new Size(300, 25), BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle };
            y += 45;

            var testBtn = new Button
            {
                Text = "▶️  Testuj uruchomienie",
                Location = new Point(150, y), Size = new Size(200, 40),
                BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            testBtn.FlatAppearance.BorderSize = 0;
            testBtn.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(_checkpointPathBox.Text, _checkpointArgsBox.Text); }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
            };

            tab.Controls.AddRange(new Control[] { pathLabel, _checkpointPathBox, pathBtn, argsLabel, _checkpointArgsBox, testBtn });
        }

        // ==================== ZAKŁADKA BACKUP ====================
        private void BuildBackupTab(TabPage tab)
        {
            int y = 20;

            var backupLabel = new Label { Text = "Folder backupu:", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            y += 25;

            _backupPathBox = new TextBox
            {
                Location = new Point(20, y), Size = new Size(450, 25),
                BackColor = inputBg, BorderStyle = BorderStyle.FixedSingle, Text = ".\\Backup"
            };
            var browseBtn = new Button
            {
                Text = "Przeglądaj", Location = new Point(480, y), Size = new Size(90, 25),
                BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            browseBtn.FlatAppearance.BorderSize = 0;
            browseBtn.Click += (s, e) => { using (var dlg = new FolderBrowserDialog()) { if (dlg.ShowDialog() == DialogResult.OK) _backupPathBox.Text = dlg.SelectedPath; } };
            y += 60;

            var backupNowBtn = new Button
            {
                Text = "💾  Wykonaj backup teraz",
                Location = new Point(150, y), Size = new Size(250, 40),
                BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            backupNowBtn.FlatAppearance.BorderSize = 0;
            backupNowBtn.Click += (s, e) =>
            {
                var source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                if (File.Exists(source))
                {
                    new Services.BackupService().CreateBackup(source, _backupPathBox.Text);
                    MessageBox.Show("✅ Backup wykonany!", "OK");
                }
                else MessageBox.Show("Brak bazy danych.");
            };

            tab.Controls.AddRange(new Control[] { backupLabel, _backupPathBox, browseBtn, backupNowBtn });
        }

        private void LoadSamplePatterns()
        {
            _patterns = new List<Pattern>
            {
                new Pattern { Id = 1, Name = "Przykładowy wzorzec", Description = "Kliknij 'Zaznacz Pattern z ekranu' aby dodać własny", IsActive = true, CreatedAt = DateTime.Now }
            };
            RefreshPatternList();
        }

        private void RefreshPatternList()
        {
            _patternListBox.Items.Clear();
            foreach (var p in _patterns)
            {
                string status = p.IsActive ? "✓" : "✗";
                _patternListBox.Items.Add($"{status} {p.Name} | {p.Description}");
            }
        }
    }

    // ==================== FORMULARZ DO ZAZNACZANIA NA EKRANIE ====================
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
            
            // Półprzezroczysta nakładka
            this.Opacity = 0.8;
            
            // Instrukcja
            var infoLabel = new Label
            {
                Text = "Zaznacz myszką obszar do zapisania jako Pattern. Naciśnij ENTER aby zatwierdzić, ESC aby anulować.",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(180, 0, 0, 0),
                Location = new Point(0, 0),
                Size = new Size(Screen.PrimaryScreen.Bounds.Width, 50),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(infoLabel);

            this.MouseDown += (s, e) =>
            {
                _startPoint = e.Location;
                _isSelecting = true;
            };

            this.MouseMove += (s, e) =>
            {
                if (_isSelecting)
                {
                    _endPoint = e.Location;
                    this.Invalidate();
                }
            };

            this.MouseUp += (s, e) =>
            {
                _isSelecting = false;
                _endPoint = e.Location;
                this.Invalidate();
            };

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && _selectedRect.Width > 10 && _selectedRect.Height > 10)
                {
                    // Wytnij zaznaczony obszar
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

                // Przyciemnienie poza zaznaczeniem
                using (var brush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    // Góra
                    e.Graphics.FillRectangle(brush, 0, 0, this.Width, y);
                    // Dół
                    e.Graphics.FillRectangle(brush, 0, y + h, this.Width, this.Height - y - h);
                    // Lewo
                    e.Graphics.FillRectangle(brush, 0, y, x, h);
                    // Prawo
                    e.Graphics.FillRectangle(brush, x + w, y, this.Width - x - w, h);
                }

                // Obramowanie zaznaczenia
                using (var pen = new Pen(Color.FromArgb(45, 165, 90), 3))
                {
                    e.Graphics.DrawRectangle(pen, _selectedRect);
                }

                // Informacja o rozmiarze
                var sizeText = $"{w} x {h} px";
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