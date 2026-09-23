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
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;

namespace SecureDesktop.Forms
{
    public class ConfigurationView : UserControl
    {
        private TextBox _adminPasswordBox;
        private TextBox _backupPathBox;
        private TextBox _checkpointPathBox;
        private TextBox _checkpointArgsBox;
        private TextBox _monitorPathBox;
        private NumericUpDown _retentionBox;
        private ListBox _patternListBox;
        private NumericUpDown _thresholdBox;
        private NumericUpDown _intervalBox;
        private NumericUpDown _marginBox;
        private CheckBox _autoStartCheck;
        private CheckBox _trayCheck;
        private PictureBox _patternPreviewBox;
        private List<Pattern> _patterns;
        private readonly DatabaseInitializer _db;
        private readonly User _currentUser;
        private readonly UserRepository _userRepo;
        private readonly ShiftRepository _shiftRepo;
        private ListView _userListView;

        // Tips
        private List<Tip> _tips;
        private ListBox _tipsListBox;
        private TextBox _tipTitleBox;
        private TextBox _tipContentBox;
        private Label _tipEditingLabel;
        private int _editingTipId = -1;

        // Shifts
        private List<Shift> _shifts;
        private ListBox _shiftsListBox;
        private TextBox _shiftNameBox;
        private TextBox _shiftPassBox;
        private Label _shiftEditingLabel;
        private int _editingShiftId = -1;

        public event Action CloseRequested;
        public event Action DataSaved;

        private bool IsHeadAdmin
        {
            get { return _currentUser != null && _currentUser.IsHeadAdmin; }
        }

        public ConfigurationView(DatabaseInitializer db, User currentUser = null)
        {
            _db = db;
            _currentUser = currentUser;
            _userRepo = new UserRepository(_db);
            _shiftRepo = new ShiftRepository(_db);
            _patterns = new List<Pattern>();
            _tips = new List<Tip>();
            _shifts = new List<Shift>();
            this.BackColor = UiTheme.Bg;
            this.ForeColor = UiTheme.TextPrimary;
            this.Font = UiFonts.Body;
            this.Dock = DockStyle.Fill;

            InitializeComponent();
            LoadPatternsFromDatabase();
            LoadTipsFromDatabase();
            if (IsHeadAdmin) LoadShiftsFromDatabase();
            LoadSettingsIntoControls();
        }

        private void InitializeComponent()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.Bg };

            header.Controls.Add(new Label
            {
                Text = Loc.T("cfg.title"),
                Font = UiFonts.H1,
                Location = new Point(0, 8),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary
            });

            header.Controls.Add(new Label
            {
                Text = Loc.T("cfg.subtitle"),
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

            var tabGeneral = MakeTab(Loc.T("cfg.tab.general"));
            var tabPatterns = MakeTab(Loc.T("cfg.tab.patterns"));
            var tabTips = MakeTab(Loc.T("cfg.tab.tips"));
            var tabCheckpoint = MakeTab(Loc.T("cfg.tab.checkpoint"));
            var tabBackup = MakeTab(Loc.T("cfg.tab.backup"));

            tabControl.TabPages.Add(tabGeneral);
            tabControl.TabPages.Add(tabPatterns);
            tabControl.TabPages.Add(tabTips);

            BuildGeneralTab(tabGeneral);
            BuildPatternsTab(tabPatterns);
            BuildTipsTab(tabTips);

            // Zakładki zastrzeżone dla HeadAdmin.
            if (IsHeadAdmin)
            {
                var tabShifts = MakeTab(Loc.T("cfg.tab.shifts"));
                var tabUsers = MakeTab(Loc.T("cfg.tab.users"));
                tabControl.TabPages.Add(tabShifts);
                tabControl.TabPages.Add(tabUsers);
                BuildShiftsTab(tabShifts);
                BuildUsersTab(tabUsers);
            }

            tabControl.TabPages.Add(tabCheckpoint);
            tabControl.TabPages.Add(tabBackup);

            BuildCheckpointTab(tabCheckpoint);
            BuildBackupTab(tabBackup);

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = UiTheme.Bg,
                Padding = new Padding(0, 14, 0, 14)
            };

            var saveBtn = RoundedButton.Primary(Loc.T("cfg.btn.save_all"), 280, 44);
            saveBtn.Location = new Point(0, 14);
            saveBtn.Click += SaveAllSettings;

            var cancelBtn = RoundedButton.Ghost(Loc.T("cfg.btn.back"), 180, 44);
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

        private static string BuildMaskedDisplayName(string firstName, string lastName)
        {
            string f = (firstName ?? "").Trim();
            string l = (lastName ?? "").Trim();
            string f2 = f.Length >= 2 ? f.Substring(0, 2) : f;
            string l2 = l.Length >= 2 ? l.Substring(0, 2) : l;
            if (string.IsNullOrEmpty(f2) && string.IsNullOrEmpty(l2)) return "";
            return f2 + "**" + l2 + "**";
        }

        // ============== OGÓLNE ==============

        private void BuildGeneralTab(TabPage tab)
        {
            int y = 20;

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.gen.pass_label"), 0, y));
            y += 24;
            _adminPasswordBox = MakeTextBox(0, y, 280, password: true);
            tab.Controls.Add(_adminPasswordBox);
            y += 36;

            tab.Controls.Add(new Label
            {
                Text = Loc.T("cfg.gen.pass_hint"),
                Font = UiFonts.Small,
                ForeColor = UiTheme.TextMuted,
                Location = new Point(0, y),
                AutoSize = true
            });
            y += 36;

            _autoStartCheck = new CheckBox
            {
                Text = Loc.T("cfg.gen.autostart"),
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
                Text = Loc.T("cfg.gen.tray"),
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
                Text = Loc.T("cfg.pat.list"),
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

            var addBtn = RoundedButton.SoftGreen(Loc.T("cfg.pat.btn_add"), 184, 40);
            addBtn.Location = new Point(0, 298);
            addBtn.Click += AddPatternFromScreen;
            tab.Controls.Add(addBtn);

            var deleteBtn = RoundedButton.SoftRed(Loc.T("cfg.pat.btn_del"), 184, 40);
            deleteBtn.Location = new Point(196, 298);
            deleteBtn.Click += DeleteSelectedPattern;
            tab.Controls.Add(deleteBtn);

            var testBtn = RoundedButton.SoftBlue(Loc.T("cfg.pat.btn_test"), 380, 42);
            testBtn.Location = new Point(0, 348);
            testBtn.Click += TestSelectedPattern;
            tab.Controls.Add(testBtn);

            tab.Controls.Add(new Label
            {
                Text = Loc.T("cfg.pat.preview"),
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
                Text = Loc.T("cfg.pat.params"),
                Font = UiFonts.H3,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(420, 268),
                AutoSize = true
            });

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.pat.threshold"), 420, 300));
            _thresholdBox = MakeNumeric(560, 296, 70, 50, 100, 85);
            tab.Controls.Add(_thresholdBox);

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.pat.interval"), 420, 334));
            _intervalBox = MakeNumeric(560, 330, 70, 100, 5000, 200);
            _intervalBox.Increment = 100;
            tab.Controls.Add(_intervalBox);

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.pat.margin"), 420, 368));
            _marginBox = MakeNumeric(560, 364, 70, 0, 100, 10);
            tab.Controls.Add(_marginBox);

            var applySettingsBtn = RoundedButton.Primary(Loc.T("cfg.pat.btn_apply"), 210, 38);
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
                    MessageBox.Show(Loc.T("cfg.pat.msg.applied"), Loc.T("common.ok"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else MessageBox.Show(Loc.T("cfg.pat.msg.select"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            tab.Controls.Add(applySettingsBtn);
        }

        // ============== WSKAZÓWKI ==============

        private void BuildTipsTab(TabPage tab)
        {
            tab.Controls.Add(new Label
            {
                Text = Loc.T("cfg.tips.title"),
                Font = UiFonts.H3,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(0, 0),
                AutoSize = true
            });

            _tipsListBox = new ListBox
            {
                Location = new Point(0, 28),
                Size = new Size(380, 340),
                Font = UiFonts.Body,
                BorderStyle = BorderStyle.None,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary
            };
            _tipsListBox.SelectedIndexChanged += (s, e) => LoadSelectedTipIntoForm();
            tab.Controls.Add(_tipsListBox);

            var newBtn = RoundedButton.SoftGreen(Loc.T("cfg.tips.btn_new"), 184, 40);
            newBtn.Location = new Point(0, 380);
            newBtn.Click += (s, e) => StartNewTip();
            tab.Controls.Add(newBtn);

            var deleteBtn = RoundedButton.SoftRed(Loc.T("cfg.tips.btn_del"), 184, 40);
            deleteBtn.Location = new Point(196, 380);
            deleteBtn.Click += DeleteSelectedTip;
            tab.Controls.Add(deleteBtn);

            _tipEditingLabel = new Label
            {
                Text = Loc.T("cfg.tips.editing_new"),
                Font = UiFonts.H3,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(420, 0),
                AutoSize = true
            };
            tab.Controls.Add(_tipEditingLabel);

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.tips.field_title"), 420, 36));
            _tipTitleBox = MakeTextBox(420, 58, 340);
            tab.Controls.Add(_tipTitleBox);

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.tips.field_content"), 420, 100));
            _tipContentBox = new TextBox
            {
                Location = new Point(420, 122),
                Size = new Size(340, 240),
                Font = UiFonts.Body,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };
            tab.Controls.Add(_tipContentBox);

            var saveTipBtn = RoundedButton.Primary(Loc.T("cfg.tips.btn_save"), 220, 42);
            saveTipBtn.Location = new Point(420, 374);
            saveTipBtn.Click += (s, e) => SaveCurrentTip();
            tab.Controls.Add(saveTipBtn);

            var resetTipBtn = RoundedButton.Ghost(Loc.T("cfg.tips.btn_clear"), 112, 42);
            resetTipBtn.Location = new Point(648, 374);
            resetTipBtn.Click += (s, e) => StartNewTip();
            tab.Controls.Add(resetTipBtn);
        }

        // ============== ZMIANY ==============

        private void BuildShiftsTab(TabPage tab)
        {
            tab.Controls.Add(new Label
            {
                Text = Loc.T("cfg.shifts.title"),
                Font = UiFonts.H3,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(0, 0),
                AutoSize = true
            });

            tab.Controls.Add(new Label
            {
                Text = Loc.T("cfg.shifts.hint"),
                Font = UiFonts.Small,
                ForeColor = UiTheme.TextMuted,
                Location = new Point(0, 26),
                AutoSize = true
            });

            _shiftsListBox = new ListBox
            {
                Location = new Point(0, 52),
                Size = new Size(380, 316),
                Font = UiFonts.Body,
                BorderStyle = BorderStyle.None,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary
            };
            _shiftsListBox.SelectedIndexChanged += (s, e) => LoadSelectedShiftIntoForm();
            tab.Controls.Add(_shiftsListBox);

            var newBtn = RoundedButton.SoftGreen(Loc.T("cfg.shifts.btn_new"), 184, 40);
            newBtn.Location = new Point(0, 380);
            newBtn.Click += (s, e) => StartNewShift();
            tab.Controls.Add(newBtn);

            var deleteBtn = RoundedButton.SoftRed(Loc.T("cfg.shifts.btn_del"), 184, 40);
            deleteBtn.Location = new Point(196, 380);
            deleteBtn.Click += DeleteSelectedShift;
            tab.Controls.Add(deleteBtn);

            _shiftEditingLabel = new Label
            {
                Text = Loc.T("cfg.shifts.editing_new"),
                Font = UiFonts.H3,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(420, 0),
                AutoSize = true
            };
            tab.Controls.Add(_shiftEditingLabel);

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.shifts.field_name"), 420, 36));
            _shiftNameBox = MakeTextBox(420, 58, 340);
            tab.Controls.Add(_shiftNameBox);

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.shifts.field_pass"), 420, 100));
            _shiftPassBox = MakeTextBox(420, 122, 340, password: true);
            tab.Controls.Add(_shiftPassBox);

            tab.Controls.Add(new Label
            {
                Text = Loc.T("cfg.shifts.pass_hint"),
                Font = UiFonts.Small,
                ForeColor = UiTheme.TextMuted,
                Location = new Point(420, 158),
                AutoSize = true
            });

            var saveShiftBtn = RoundedButton.Primary(Loc.T("cfg.shifts.btn_save"), 220, 42);
            saveShiftBtn.Location = new Point(420, 200);
            saveShiftBtn.Click += (s, e) => SaveCurrentShift();
            tab.Controls.Add(saveShiftBtn);

            var resetShiftBtn = RoundedButton.Ghost(Loc.T("cfg.shifts.btn_clear"), 112, 42);
            resetShiftBtn.Location = new Point(648, 200);
            resetShiftBtn.Click += (s, e) => StartNewShift();
            tab.Controls.Add(resetShiftBtn);
        }

        private void LoadShiftsFromDatabase()
        {
            try
            {
                _shifts = _shiftRepo.GetAllShifts();
            }
            catch (Exception ex)
            {
                _shifts = new List<Shift>();
                System.Diagnostics.Debug.WriteLine("Load shifts error: " + ex.Message);
            }
            RefreshShiftsList();
        }

        private void RefreshShiftsList()
        {
            if (_shiftsListBox == null) return;
            _shiftsListBox.Items.Clear();
            foreach (var s in _shifts)
                _shiftsListBox.Items.Add(s.Name ?? "(—)");
        }

        private void StartNewShift()
        {
            _editingShiftId = -1;
            _shiftsListBox.ClearSelected();
            _shiftNameBox.Text = "";
            _shiftPassBox.Text = "";
            _shiftEditingLabel.Text = Loc.T("cfg.shifts.editing_new");
        }

        private void LoadSelectedShiftIntoForm()
        {
            if (_shiftsListBox.SelectedIndex < 0 || _shiftsListBox.SelectedIndex >= _shifts.Count) return;
            var s = _shifts[_shiftsListBox.SelectedIndex];
            _editingShiftId = s.Id;
            _shiftNameBox.Text = s.Name ?? "";
            _shiftPassBox.Text = "";
            _shiftEditingLabel.Text = Loc.T("cfg.shifts.editing_edit");
        }

        private void SaveCurrentShift()
        {
            string name = (_shiftNameBox.Text ?? "").Trim();
            string pass = _shiftPassBox.Text ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(Loc.T("cfg.shifts.err_no_name"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_editingShiftId == -1)
            {
                if (string.IsNullOrEmpty(pass))
                {
                    MessageBox.Show(Loc.T("cfg.shifts.err_no_pass_new"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var newShift = new Shift { Name = name };
                newShift.Salt = SecurityHelper.GenerateSalt();
                newShift.PasswordHash = SecurityHelper.HashPassword(pass, newShift.Salt);
                _shiftRepo.AddShift(newShift);
            }
            else
            {
                var existing = _shifts.FirstOrDefault(x => x.Id == _editingShiftId);
                if (existing != null)
                {
                    existing.Name = name;
                    _shiftRepo.UpdateShift(existing);
                    if (!string.IsNullOrEmpty(pass))
                        _shiftRepo.SetPassword(existing.Id, pass);
                }
            }

            LoadShiftsFromDatabase();
            StartNewShift();
            var h = DataSaved;
            if (h != null) h();
            MessageBox.Show(Loc.T("cfg.shifts.saved"), Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DeleteSelectedShift(object sender, EventArgs e)
        {
            if (_shiftsListBox.SelectedIndex < 0 || _shiftsListBox.SelectedIndex >= _shifts.Count)
            {
                MessageBox.Show(Loc.T("cfg.shifts.select_to_delete"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(Loc.T("cfg.shifts.del_confirm"), Loc.T("common.confirm"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _shiftRepo.DeleteShift(_shifts[_shiftsListBox.SelectedIndex].Id);
                LoadShiftsFromDatabase();
                StartNewShift();
                var h = DataSaved;
                if (h != null) h();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ============== TIPS HELPERS ==============

        private void LoadTipsFromDatabase()
        {
            try
            {
                var data = _db.GetData();
                _tips = data != null && data.Tips != null ? data.Tips : new List<Tip>();
            }
            catch (Exception ex)
            {
                _tips = new List<Tip>();
                System.Diagnostics.Debug.WriteLine("Load tips error: " + ex.Message);
            }
            RefreshTipsList();
        }

        private void SaveTipsToDatabase()
        {
            try
            {
                if (_db == null) return;
                var data = _db.GetData();
                data.Tips = _tips;
                if (_tips.Count > 0) data.NextTipId = _tips.Max(t => t.Id) + 1;
                else data.NextTipId = 1;
                _db.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("cfg.msg.save_tips_err") + ex.Message, Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshTipsList()
        {
            if (_tipsListBox == null) return;
            _tipsListBox.Items.Clear();
            foreach (var t in _tips)
            {
                string title = string.IsNullOrWhiteSpace(t.Title) ? "(—)" : t.Title.ToUpperInvariant();
                _tipsListBox.Items.Add(title);
            }
        }

        private void StartNewTip()
        {
            _editingTipId = -1;
            _tipsListBox.ClearSelected();
            _tipTitleBox.Text = "";
            _tipContentBox.Text = "";
            _tipEditingLabel.Text = Loc.T("cfg.tips.editing_new");
        }

        private void LoadSelectedTipIntoForm()
        {
            if (_tipsListBox.SelectedIndex < 0 || _tipsListBox.SelectedIndex >= _tips.Count) return;
            var t = _tips[_tipsListBox.SelectedIndex];
            _editingTipId = t.Id;
            _tipTitleBox.Text = t.Title ?? "";
            _tipContentBox.Text = t.Content ?? "";
            _tipEditingLabel.Text = Loc.T("cfg.tips.editing_edit");
        }

        private void SaveCurrentTip()
        {
            string title = (_tipTitleBox.Text ?? "").Trim();
            string content = (_tipContentBox.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show(Loc.T("cfg.tips.err_no_title"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show(Loc.T("cfg.tips.err_no_content"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_editingTipId == -1)
            {
                int newId = _tips.Count > 0 ? _tips.Max(t => t.Id) + 1 : 1;
                _tips.Add(new Tip
                {
                    Id = newId,
                    Title = title,
                    Content = content,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                var t = _tips.FirstOrDefault(x => x.Id == _editingTipId);
                if (t != null)
                {
                    t.Title = title;
                    t.Content = content;
                    t.UpdatedAt = DateTime.Now;
                }
            }

            SaveTipsToDatabase();
            RefreshTipsList();
            var h = DataSaved;
            if (h != null) h();
            StartNewTip();
            MessageBox.Show(Loc.T("cfg.tips.saved"), Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DeleteSelectedTip(object sender, EventArgs e)
        {
            if (_tipsListBox.SelectedIndex < 0 || _tipsListBox.SelectedIndex >= _tips.Count)
            {
                MessageBox.Show(Loc.T("cfg.tips.select_to_delete"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(Loc.T("cfg.tips.del_confirm"), Loc.T("common.confirm"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _tips.RemoveAt(_tipsListBox.SelectedIndex);
            SaveTipsToDatabase();
            RefreshTipsList();
            var h = DataSaved;
            if (h != null) h();
            StartNewTip();
        }

        // ============== TEST WZORCA ==============

        private void TestSelectedPattern(object sender, EventArgs e)
        {
            if (_patternListBox.SelectedIndex < 0 || _patternListBox.SelectedIndex >= _patterns.Count)
            {
                MessageBox.Show(Loc.T("cfg.pat.msg.select_test"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var pattern = _patterns[_patternListBox.SelectedIndex];
            if (pattern.ImageData == null || pattern.ImageData.Length == 0)
            {
                MessageBox.Show(Loc.T("cfg.pat.msg.no_image"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var parentForm = this.FindForm();
            if (parentForm != null) parentForm.WindowState = FormWindowState.Minimized;
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
                MessageBox.Show(Loc.T("cfg.pat.msg.test_err") + (result == null ? Loc.T("cfg.pat.msg.unknown") : result.Error),
                    Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            double gap12 = result.Score - result.SecondScore;
            string verdict;
            if (result.Found && gap12 >= 0.15) verdict = Loc.T("cfg.pat.verdict_good");
            else if (result.Found && gap12 >= 0.05) verdict = Loc.T("cfg.pat.verdict_ok");
            else if (result.Found) verdict = Loc.T("cfg.pat.verdict_weak");
            else if (result.Score >= 0.6) verdict = Loc.T("cfg.pat.verdict_similar");
            else verdict = Loc.T("cfg.pat.verdict_notvisible");

            string headerLine = Loc.T("cfg.pat.test_header", pattern.Name, result.PatternWidth, result.PatternHeight, result.StdDev.ToString("F1"));
            string scoresLine = "Top-1: " + result.Score.ToString("F3") + "   Top-2: " + result.SecondScore.ToString("F3") +
                                "   Top-3: " + result.ThirdScore.ToString("F3") +
                                "   " + (Loc.IsEnglish ? "Threshold" : "Próg") + ": " + result.Threshold.ToString("F2");
            string msg = headerLine + "\n" + scoresLine + "\n" + verdict;

            using (var preview = new Form
            {
                Text = Loc.T("cfg.pat.preview") + " — " + pattern.Name,
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
                AddCrop(stripPanel, Loc.IsEnglish ? "PATTERN" : "WZORZEC", pattern.ImageData, xpos, ref xpos);
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

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.cp.path"), 0, y));
            y += 24;

            _checkpointPathBox = MakeTextBox(0, y, 380);
            _checkpointPathBox.Text = "notepad.exe";
            tab.Controls.Add(_checkpointPathBox);

            var pathBtn = RoundedButton.Ghost(Loc.T("cfg.cp.btn_browse"), 120, 32);
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

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.cp.args"), 0, y));
            y += 24;

            _checkpointArgsBox = MakeTextBox(0, y, 300);
            tab.Controls.Add(_checkpointArgsBox);
            y += 56;

            var testBtn = RoundedButton.Primary(Loc.T("cfg.cp.btn_test"), 240, 44);
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
                catch (Exception ex) { MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            tab.Controls.Add(testBtn);
        }

        // ============== BACKUP ==============

        private void BuildBackupTab(TabPage tab)
        {
            int y = 20;

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.bk.folder"), 0, y));
            y += 24;

            _backupPathBox = MakeTextBox(0, y, 380);
            _backupPathBox.Text = ".\\Backup";
            tab.Controls.Add(_backupPathBox);

            var backupBrowseBtn = RoundedButton.Ghost(Loc.T("cfg.cp.btn_browse"), 120, 32);
            backupBrowseBtn.Location = new Point(392, y);
            backupBrowseBtn.Click += (s, ev) =>
            {
                using (var dlg = new FolderBrowserDialog())
                    if (dlg.ShowDialog() == DialogResult.OK) _backupPathBox.Text = dlg.SelectedPath;
            };
            tab.Controls.Add(backupBrowseBtn);
            y += 44;

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.bk.file"), 0, y));
            y += 24;

            _monitorPathBox = MakeTextBox(0, y, 380);
            tab.Controls.Add(_monitorPathBox);

            var monitorBrowseBtn = RoundedButton.Ghost(Loc.T("cfg.cp.btn_browse"), 120, 32);
            monitorBrowseBtn.Location = new Point(392, y);
            monitorBrowseBtn.Click += (s, ev) =>
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = Loc.IsEnglish ? "All files|*.*" : "Wszystkie pliki|*.*";
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        _monitorPathBox.Text = dlg.FileName;
                        try { new Services.FileMonitorService(_db).ResetBaseline(); } catch { }
                    }
                }
            };
            tab.Controls.Add(monitorBrowseBtn);
            y += 56;

            tab.Controls.Add(MakeFieldLabel(Loc.T("cfg.bk.retention"), 0, y));
            y += 24;

            _retentionBox = MakeNumeric(0, y, 90, 0, 3650, 30);
            _retentionBox.Increment = 1;
            tab.Controls.Add(_retentionBox);
            y += 36;

            tab.Controls.Add(new Label
            {
                Text = Loc.T("cfg.bk.retention_hint"),
                Font = UiFonts.Small,
                ForeColor = UiTheme.TextMuted,
                Location = new Point(0, y),
                Size = new Size(600, 40),
                AutoSize = false
            });
            y += 52;

            var backupNowBtn = RoundedButton.Primary(Loc.T("cfg.bk.btn_run"), 260, 44);
            backupNowBtn.Location = new Point(0, y);
            backupNowBtn.Click += (s, ev) =>
            {
                string sourcePath = _monitorPathBox.Text;
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    MessageBox.Show(Loc.T("cfg.bk.err_no_file"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    var backupService = new Services.BackupService();
                    string result = backupService.CreateBackup(sourcePath, _backupPathBox.Text);
                    new Services.FileMonitorService(_db).SaveBaseline(sourcePath);
                    MessageBox.Show(Loc.T("cfg.bk.done") + "\n\n" + result, Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            tab.Controls.Add(backupNowBtn);

                       var purgeNowBtn = RoundedButton.SoftRed(Loc.T("cfg.bk.btn_purge_now"), 260, 44);
            purgeNowBtn.Location = new Point(276, y);
            purgeNowBtn.Click += (s, ev) =>
            {
                try
                {
                    int days = (int)_retentionBox.Value;
                    if (days <= 0)
                    {
                        MessageBox.Show(Loc.T("cfg.bk.retention_hint"), Loc.T("common.info"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    string folder = _backupPathBox.Text;
                    if (!Path.IsPathRooted(folder))
                        folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);

                    var svc = new Services.BackupService();
                    int deleted = svc.PurgeOldBackups(folder, days);

                    // === Log zdarzenia do dziennika ===
                    if (deleted > 0)
                    {
                        try
                        {
                            new EventLogRepository(_db).Create(new EventLog
                            {
                                UserId = _currentUser != null ? (int?)_currentUser.Id : null,
                                IdentificationNumber = _currentUser != null
                                    ? _currentUser.IdentificationNumber
                                    : "SYSTEM",
                                OperationName = "BackupPurge",
                                Result = "Success",
                                Severity = "Info",
                                Description = Loc.T("log.backup_purge_manual", deleted)
                            });
                        }
                        catch { }
                    }

                    if (deleted > 0)
                        MessageBox.Show(string.Format(Loc.T("cfg.bk.purge_done"), deleted),
                            Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show(string.Format(Loc.T("cfg.bk.purge_none"), days),
                            Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Loc.T("common.error") + ": " + ex.Message, Loc.T("common.error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            tab.Controls.Add(purgeNowBtn);
        }

        // ============== UŻYTKOWNICY ==============

               private void BuildUsersTab(TabPage tab)
        {
            tab.Controls.Add(new Label
            {
                Text = Loc.T("cfg.usr.title"),
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
            _userListView.Columns.Add(Loc.T("cfg.usr.col_id"), 40);
            _userListView.Columns.Add(Loc.T("cfg.usr.col_display"), 150);
            _userListView.Columns.Add(Loc.T("cfg.usr.col_pin"), 110);
            _userListView.Columns.Add(Loc.T("cfg.usr.col_shift"), 110);
            _userListView.Columns.Add(Loc.T("cfg.usr.col_role"), 110);
            _userListView.Columns.Add(Loc.T("cfg.usr.col_active"), 60);
            _userListView.DoubleClick += (s, e) => EditUser(s, e);
            RefreshUserList();
            tab.Controls.Add(_userListView);

            var addBtn = RoundedButton.Primary(Loc.T("cfg.usr.btn_add"), 180, 40);
            addBtn.Location = new Point(580, 28);
            addBtn.Click += AddUser;
            tab.Controls.Add(addBtn);

            var editBtn = RoundedButton.SoftGreen(Loc.T("cfg.usr.btn_edit"), 180, 40);
            editBtn.Location = new Point(580, 76);
            editBtn.Click += EditUser;
            tab.Controls.Add(editBtn);

            var deleteBtn = RoundedButton.SoftRed(Loc.T("cfg.usr.btn_deactivate"), 180, 40);
            deleteBtn.Location = new Point(580, 124);
            deleteBtn.Click += DeleteUser;
            tab.Controls.Add(deleteBtn);

            // Uwaga: przycisk "Zmień rolę" (ToggleUserRole) został usunięty —
            // zmiana roli odbywa się w dialogu edycji użytkownika.

            var importBtn = RoundedButton.SoftGreen(Loc.T("cfg.usr.btn_import"), 180, 40);
            importBtn.Location = new Point(580, 186);
            importBtn.Click += ImportUsersFromXlsx;
            tab.Controls.Add(importBtn);

            var exportBtn = RoundedButton.SoftBlue(Loc.T("cfg.usr.btn_export"), 180, 40);
            exportBtn.Location = new Point(580, 234);
            exportBtn.Click += ExportUsersToXlsx;
            tab.Controls.Add(exportBtn);
        }

        private void RefreshUserList()
        {
            if (_userListView == null || _userRepo == null) return;
            _userListView.Items.Clear();

            var shifts = _shiftRepo != null ? _shiftRepo.GetAllShifts() : new List<Shift>();
            foreach (var u in _userRepo.GetAllUsers())
            {
                var shift = u.ShiftId.HasValue ? shifts.FirstOrDefault(s => s.Id == u.ShiftId.Value) : null;
                string display = !string.IsNullOrWhiteSpace(u.DisplayName) ? u.DisplayName : u.IdentificationNumber;

                string roleTxt = u.IsHeadAdmin ? Loc.T("cfg.usr.role_headadmin")
                                : (u.IsAdmin ? Loc.T("cfg.usr.role_admin") : Loc.T("cfg.usr.role_user"));

                var item = new ListViewItem(u.Id.ToString());
                item.SubItems.Add(display);
                item.SubItems.Add(u.IdentificationNumber);
                item.SubItems.Add(shift != null ? shift.Name : "—");
                item.SubItems.Add(roleTxt);
                item.SubItems.Add(u.IsActive ? Loc.T("cfg.usr.yes") : Loc.T("cfg.usr.no"));
                _userListView.Items.Add(item);
            }
        }

        private void AddUser(object sender, EventArgs e)
        {
            var shifts = _shiftRepo != null ? _shiftRepo.GetAllShifts() : new List<Shift>();
            if (shifts.Count == 0)
            {
                MessageBox.Show(Loc.T("cfg.usr.no_shifts"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new Form
            {
                Text = Loc.T("cfg.usr.dlg_title"),
                Size = new Size(420, 460),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = UiTheme.Bg,
                Font = UiFonts.Body,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                int y = 24;

                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_ident"), 24, y));
                y += 24;
                var idBox = MakeTextBox(24, y, 356);
                dialog.Controls.Add(idBox);
                y += 40;

                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_first_name"), 24, y));
                y += 24;
                var fnBox = MakeTextBox(24, y, 356);
                dialog.Controls.Add(fnBox);
                y += 40;

                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_last_name"), 24, y));
                y += 24;
                var lnBox = MakeTextBox(24, y, 356);
                dialog.Controls.Add(lnBox);
                y += 40;

                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_shift"), 24, y));
                y += 24;
                var shiftCombo = new ComboBox
                {
                    Location = new Point(24, y),
                    Size = new Size(356, 30),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = UiFonts.BodyLarge,
                    BackColor = UiTheme.Surface
                };
                foreach (var s in shifts) shiftCombo.Items.Add(s.Name);
                shiftCombo.SelectedIndex = 0;
                dialog.Controls.Add(shiftCombo);
                y += 40;

                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_role"), 24, y));
                y += 24;
                var roleCombo = new ComboBox
                {
                    Location = new Point(24, y),
                    Size = new Size(356, 30),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = UiFonts.BodyLarge,
                    BackColor = UiTheme.Surface
                };
                roleCombo.Items.Add(Loc.T("cfg.usr.role_user"));
                roleCombo.Items.Add(Loc.T("cfg.usr.role_admin"));
                roleCombo.Items.Add(Loc.T("cfg.usr.role_headadmin"));
                roleCombo.SelectedIndex = 0;
                dialog.Controls.Add(roleCombo);
                y += 46;

                var okBtn = RoundedButton.Primary(Loc.T("cfg.usr.dlg_add"), 130, 42);
                okBtn.Location = new Point(120, y);
                okBtn.Click += (s, args) =>
                {
                    if (string.IsNullOrWhiteSpace(idBox.Text))
                    {
                        MessageBox.Show(Loc.T("cfg.usr.err_empty_ident"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (shiftCombo.SelectedIndex < 0)
                    {
                        MessageBox.Show(Loc.T("cfg.usr.err_no_shift"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (_userRepo.GetByIdentificationNumber(idBox.Text) != null)
                    {
                        MessageBox.Show(Loc.T("cfg.usr.err_exists"), Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    bool isHead = roleCombo.SelectedIndex == 2;
                    bool isAdm = roleCombo.SelectedIndex == 1 || isHead;

                    var selectedShift = shifts[shiftCombo.SelectedIndex];
                    var u = new User
                    {
                        IdentificationNumber = idBox.Text.Trim(),
                        Salt = "",
                        PasswordHash = "",
                        FirstName = (fnBox.Text ?? "").Trim(),
                        LastName = (lnBox.Text ?? "").Trim(),
                        IsAdmin = isAdm,
                        IsHeadAdmin = isHead,
                        IsActive = true,
                        ShiftId = selectedShift.Id
                    };
                    u.DisplayName = BuildMaskedDisplayName(u.FirstName, u.LastName);

                    _userRepo.AddUser(u);
                    RefreshUserList();
                    dialog.Close();
                };

                var cancelBtn = RoundedButton.Ghost(Loc.T("cfg.usr.dlg_cancel"), 120, 42);
                cancelBtn.Location = new Point(260, y);
                cancelBtn.Click += (s, args) => dialog.Close();

                dialog.Controls.Add(okBtn);
                dialog.Controls.Add(cancelBtn);

                dialog.ShowDialog(this);
            }
        }

        // ============== EDYCJA UŻYTKOWNIKA ==============

                private void EditUser(object sender, EventArgs e)
        {
            if (_userListView.SelectedItems.Count == 0)
            {
                MessageBox.Show(Loc.T("cfg.usr.select_role"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int userId = int.Parse(_userListView.SelectedItems[0].Text);
            var user = _userRepo.GetById(userId);
            if (user == null) return;

            var shifts = _shiftRepo != null ? _shiftRepo.GetAllShifts() : new List<Shift>();
            if (shifts.Count == 0)
            {
                MessageBox.Show(Loc.T("cfg.usr.no_shifts"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool isDefaultAdmin = string.Equals(user.IdentificationNumber, "admin", StringComparison.OrdinalIgnoreCase);

            using (var dialog = new Form
            {
                Text = Loc.T("cfg.usr.edit_dlg_title"),
                Size = new Size(460, 700),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = UiTheme.Bg,
                Font = UiFonts.Body,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                int y = 20;

                // === PIN ===
                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_ident"), 24, y));
                y += 24;
                var idBox = MakeTextBox(24, y, 396);
                idBox.Text = user.IdentificationNumber ?? "";
                idBox.ReadOnly = isDefaultAdmin;
                if (isDefaultAdmin) idBox.BackColor = UiTheme.SurfaceAlt;
                dialog.Controls.Add(idBox);
                y += 40;

                // === Imię ===
                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_first_name"), 24, y));
                y += 24;
                var fnBox = MakeTextBox(24, y, 396);
                fnBox.Text = user.FirstName ?? "";
                dialog.Controls.Add(fnBox);
                y += 40;

                // === Nazwisko ===
                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_last_name"), 24, y));
                y += 24;
                var lnBox = MakeTextBox(24, y, 396);
                lnBox.Text = user.LastName ?? "";
                dialog.Controls.Add(lnBox);
                y += 40;

                // === DisplayName (podgląd auto) ===
                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_display"), 24, y));
                y += 24;
                var displayBox = MakeTextBox(24, y, 396);
                displayBox.ReadOnly = true;
                displayBox.BackColor = UiTheme.SurfaceAlt;
                displayBox.Text = BuildMaskedDisplayName(user.FirstName, user.LastName);
                if (string.IsNullOrEmpty(displayBox.Text)) displayBox.Text = user.DisplayName ?? "";
                dialog.Controls.Add(displayBox);
                y += 40;

                Action refreshDisplay = () =>
                {
                    string masked = BuildMaskedDisplayName(fnBox.Text, lnBox.Text);
                    displayBox.Text = masked;
                };
                fnBox.TextChanged += (s, args) => refreshDisplay();
                lnBox.TextChanged += (s, args) => refreshDisplay();

                // === Zmiana ===
                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_shift"), 24, y));
                y += 24;
                var shiftCombo = new ComboBox
                {
                    Location = new Point(24, y),
                    Size = new Size(396, 30),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = UiFonts.BodyLarge,
                    BackColor = UiTheme.Surface
                };
                foreach (var s in shifts) shiftCombo.Items.Add(s.Name);
                if (user.ShiftId.HasValue)
                {
                    int idx = shifts.FindIndex(s => s.Id == user.ShiftId.Value);
                    shiftCombo.SelectedIndex = idx >= 0 ? idx : 0;
                }
                else shiftCombo.SelectedIndex = 0;
                dialog.Controls.Add(shiftCombo);
                y += 40;

                // === Rola ===
                dialog.Controls.Add(MakeFieldLabel(Loc.T("cfg.usr.dlg_role"), 24, y));
                y += 24;
                var roleCombo = new ComboBox
                {
                    Location = new Point(24, y),
                    Size = new Size(396, 30),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = UiFonts.BodyLarge,
                    BackColor = UiTheme.Surface,
                    Enabled = !isDefaultAdmin
                };
                roleCombo.Items.Add(Loc.T("cfg.usr.role_user"));
                roleCombo.Items.Add(Loc.T("cfg.usr.role_admin"));
                roleCombo.Items.Add(Loc.T("cfg.usr.role_headadmin"));
                if (user.IsHeadAdmin) roleCombo.SelectedIndex = 2;
                else if (user.IsAdmin) roleCombo.SelectedIndex = 1;
                else roleCombo.SelectedIndex = 0;
                dialog.Controls.Add(roleCombo);
                y += 40;

                // === Aktywne konto ===
                var activeCheck = new CheckBox
                {
                    Text = Loc.T("cfg.usr.active"),
                    Font = UiFonts.Body,
                    Location = new Point(24, y),
                    AutoSize = true,
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = UiTheme.TextPrimary,
                    Checked = user.IsActive,
                    Enabled = !isDefaultAdmin
                };
                dialog.Controls.Add(activeCheck);
                y += 34;

                // === Hasło indywidualne ===
                var indPassCheck = new CheckBox
                {
                    Text = Loc.T("cfg.usr.dlg_individual"),
                    Font = UiFonts.Body,
                    Location = new Point(24, y),
                    AutoSize = true,
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = UiTheme.TextPrimary,
                    Checked = user.UseIndividualPassword
                };
                dialog.Controls.Add(indPassCheck);
                y += 24;

                dialog.Controls.Add(new Label
                {
                    Text = Loc.T("cfg.usr.dlg_individual_hint"),
                    Font = UiFonts.Small,
                    ForeColor = UiTheme.TextMuted,
                    Location = new Point(24, y),
                    Size = new Size(396, 32),
                    AutoSize = false
                });
                y += 36;

                // === Pola haseł (pokazywane warunkowo) ===
                var passLabel = MakeFieldLabel(Loc.T("cfg.usr.dlg_new_pass"), 24, y);
                passLabel.Visible = indPassCheck.Checked;
                dialog.Controls.Add(passLabel);
                y += 24;

                var passBox = MakeTextBox(24, y, 396, password: true);
                passBox.Visible = indPassCheck.Checked;
                dialog.Controls.Add(passBox);
                y += 40;

                var passConfirmLabel = MakeFieldLabel(Loc.T("cfg.usr.dlg_confirm_pass"), 24, y);
                passConfirmLabel.Visible = indPassCheck.Checked;
                dialog.Controls.Add(passConfirmLabel);
                y += 24;

                var passConfirmBox = MakeTextBox(24, y, 396, password: true);
                passConfirmBox.Visible = indPassCheck.Checked;
                dialog.Controls.Add(passConfirmBox);
                y += 44;

                // Toggle widoczności pól haseł
                indPassCheck.CheckedChanged += (s, args) =>
                {
                    bool vis = indPassCheck.Checked;
                    passLabel.Visible = vis;
                    passBox.Visible = vis;
                    passConfirmLabel.Visible = vis;
                    passConfirmBox.Visible = vis;
                    if (!vis)
                    {
                        passBox.Text = "";
                        passConfirmBox.Text = "";
                    }
                };

                // === Save/Cancel ===
                var saveBtn = RoundedButton.Primary(Loc.T("cfg.usr.dlg_save"), 150, 42);
                saveBtn.Location = new Point(140, y);
                saveBtn.Click += (s, args) =>
                {
                    string newPin = (idBox.Text ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(newPin))
                    {
                        MessageBox.Show(Loc.T("cfg.usr.err_empty_ident"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (shiftCombo.SelectedIndex < 0)
                    {
                        MessageBox.Show(Loc.T("cfg.usr.err_no_shift"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (!string.Equals(newPin, user.IdentificationNumber, StringComparison.Ordinal))
                    {
                        var clash = _userRepo.GetAllUsers()
                            .FirstOrDefault(u => u.Id != user.Id &&
                                                 string.Equals(u.IdentificationNumber, newPin, StringComparison.Ordinal));
                        if (clash != null)
                        {
                            MessageBox.Show(Loc.T("cfg.usr.edit_err_exists"), Loc.T("common.error"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    // === Walidacja hasła indywidualnego ===
                    bool newUseInd = indPassCheck.Checked;
                    string newPassPlain = (passBox.Text ?? "");
                    string newPassConfirm = (passConfirmBox.Text ?? "");

                    bool hasExistingInd = user.UseIndividualPassword &&
                                          !string.IsNullOrEmpty(user.PasswordHash) &&
                                          !string.IsNullOrEmpty(user.Salt);

                    if (newUseInd)
                    {
                        // Jeśli użytkownik wpisał nowe hasło — waliduj.
                        if (!string.IsNullOrEmpty(newPassPlain) || !string.IsNullOrEmpty(newPassConfirm))
                        {
                            if (newPassPlain.Length < 4)
                            {
                                MessageBox.Show(Loc.T("cfg.usr.err_pass_short"), Loc.T("common.info"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return;
                            }
                            if (!string.Equals(newPassPlain, newPassConfirm, StringComparison.Ordinal))
                            {
                                MessageBox.Show(Loc.T("cfg.usr.err_pass_mismatch"), Loc.T("common.info"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return;
                            }
                        }
                        else if (!hasExistingInd)
                        {
                            // Zaznaczone, brak hasła w polach, brak istniejącego hasła → wymagane.
                            MessageBox.Show(Loc.T("cfg.usr.err_pass_empty"), Loc.T("common.info"),
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                    }

                    // === Zapis ===
                    var selectedShift = shifts[shiftCombo.SelectedIndex];

                    user.IdentificationNumber = newPin;
                    user.FirstName = (fnBox.Text ?? "").Trim();
                    user.LastName = (lnBox.Text ?? "").Trim();
                    user.DisplayName = BuildMaskedDisplayName(user.FirstName, user.LastName);
                    user.ShiftId = selectedShift.Id;

                    if (!isDefaultAdmin)
                    {
                        bool isHead = roleCombo.SelectedIndex == 2;
                        bool isAdm = roleCombo.SelectedIndex == 1 || isHead;
                        user.IsHeadAdmin = isHead;
                        user.IsAdmin = isAdm;
                        user.IsActive = activeCheck.Checked;
                    }

                    // Hasło indywidualne
                    if (newUseInd)
                    {
                        user.UseIndividualPassword = true;
                        if (!string.IsNullOrEmpty(newPassPlain))
                        {
                            // Ustaw nowe hasło.
                            user.Salt = SecurityHelper.GenerateSalt();
                            user.PasswordHash = SecurityHelper.HashPassword(newPassPlain, user.Salt);
                        }
                        // else: zostaw istniejący hash/salt (już w user.*)
                    }
                    else
                    {
                        user.UseIndividualPassword = false;
                        user.PasswordHash = null;
                        user.Salt = null;
                    }

                    _userRepo.UpdateUser(user);
                    RefreshUserList();
                    var h = DataSaved;
                    if (h != null) h();
                    dialog.Close();
                };

                var cancelBtn = RoundedButton.Ghost(Loc.T("cfg.usr.dlg_cancel"), 150, 42);
                cancelBtn.Location = new Point(300, y);
                cancelBtn.Click += (s, args) => dialog.Close();

                dialog.Controls.Add(saveBtn);
                dialog.Controls.Add(cancelBtn);

                dialog.AcceptButton = null;
                dialog.ShowDialog(this);
            }
        }

        private void DeleteUser(object sender, EventArgs e)
        {
            if (_userListView.SelectedItems.Count == 0)
            {
                MessageBox.Show(Loc.T("cfg.usr.select_del"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var userId = int.Parse(_userListView.SelectedItems[0].Text);
            var user = _userRepo.GetAllUsers().FirstOrDefault(u => u.Id == userId);
            if (user == null) return;
            if (user.IdentificationNumber == "admin")
            {
                MessageBox.Show(Loc.T("cfg.usr.err_cant_del_admin"), Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show(Loc.T("cfg.usr.del_confirm", user.IdentificationNumber),
                Loc.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _userRepo.DeleteUser(userId);
                RefreshUserList();
            }
        }

        private void ToggleUserRole(object sender, EventArgs e)
        {
            if (_userListView.SelectedItems.Count == 0)
            {
                MessageBox.Show(Loc.T("cfg.usr.select_role"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var userId = int.Parse(_userListView.SelectedItems[0].Text);
            var user = _userRepo.GetAllUsers().FirstOrDefault(u => u.Id == userId);
            if (user == null) return;
            if (user.IdentificationNumber == "admin")
            {
                MessageBox.Show(Loc.T("cfg.usr.err_cant_change_admin"), Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Cykl: User -> Admin -> HeadAdmin -> User
            if (user.IsHeadAdmin) { user.IsHeadAdmin = false; user.IsAdmin = false; }
            else if (user.IsAdmin) { user.IsHeadAdmin = true; user.IsAdmin = true; }
            else { user.IsAdmin = true; user.IsHeadAdmin = false; }

            _userRepo.UpdateUser(user);
            RefreshUserList();
            MessageBox.Show(Loc.T("cfg.usr.role_changed", user.RoleName),
                Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ============== IMPORT / EXPORT XLSX ==============

        private void ImportUsersFromXlsx(object sender, EventArgs e)
        {
            string filePath = null;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = Loc.T("cfg.usr.import_dlg_title");
                dlg.Filter = Loc.T("cfg.usr.import_filter");
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                filePath = dlg.FileName;
            }

            Services.ImportResult res = null;
            try
            {
                res = Services.UserExcelService.ImportFromXlsx(filePath, _db);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("common.error") + ": " + ex.Message,
                    Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RefreshUserList();
            var h = DataSaved;
            if (h != null) h();

            string msg = string.Format(Loc.T("cfg.usr.import_done"), res.Added, res.Updated, res.Skipped);

            if (res.Errors != null && res.Errors.Count > 0)
            {
                string errors = string.Join("\n", res.Errors.Take(20));
                if (res.Errors.Count > 20) errors += "\n... (+" + (res.Errors.Count - 20) + ")";
                MessageBox.Show(msg + string.Format(Loc.T("cfg.usr.import_errors"), errors),
                    Loc.T("cfg.usr.import_err_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(msg, Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportUsersToXlsx(object sender, EventArgs e)
        {
            string filePath = null;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = Loc.T("cfg.usr.export_dlg_title");
                dlg.Filter = Loc.T("cfg.usr.import_filter");
                dlg.FileName = "users_" + DateTime.Now.ToString("yyyy-MM-dd") + ".xlsx";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                filePath = dlg.FileName;
            }

            try
            {
                var res = Services.UserExcelService.ExportToXlsx(filePath, _db);
                MessageBox.Show(string.Format(Loc.T("cfg.usr.export_done"), res.Written, res.Path),
                    Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("common.error") + ": " + ex.Message,
                    Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                string s;
                int iv;

                if (data.Settings.TryGetValue("AutoStart", out s) && _autoStartCheck != null)
                    _autoStartCheck.Checked = bool.TryParse(s, out b) && b;
                if (data.Settings.TryGetValue("MinimizeToTray", out s) && _trayCheck != null)
                    _trayCheck.Checked = bool.TryParse(s, out b) && b;

                if (data.Settings.TryGetValue("BackupPath", out s) && _backupPathBox != null) _backupPathBox.Text = s;
                if (data.Settings.TryGetValue("MonitoredFile", out s) && _monitorPathBox != null) _monitorPathBox.Text = s;
                if (data.Settings.TryGetValue("CheckpointPath", out s) && _checkpointPathBox != null) _checkpointPathBox.Text = s;
                if (data.Settings.TryGetValue("CheckpointArgs", out s) && _checkpointArgsBox != null) _checkpointArgsBox.Text = s;

                if (data.Settings.TryGetValue("PatternThreshold", out s) && _thresholdBox != null && int.TryParse(s, out iv))
                    _thresholdBox.Value = Math.Max(_thresholdBox.Minimum, Math.Min(_thresholdBox.Maximum, iv));

                if (data.Settings.TryGetValue("SearchInterval", out s) && _intervalBox != null && int.TryParse(s, out iv))
                    _intervalBox.Value = Math.Max(_intervalBox.Minimum, Math.Min(_intervalBox.Maximum, iv));

                if (data.Settings.TryGetValue("BackupRetentionDays", out s) && _retentionBox != null &&
                    int.TryParse(s, out iv))
                    _retentionBox.Value = Math.Max(_retentionBox.Minimum, Math.Min(_retentionBox.Maximum, iv));
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
                            Id = 1,
                            Name = Loc.IsEnglish ? "Sample pattern" : "Przykładowy wzorzec",
                            Description = Loc.IsEnglish ? "Click 'Capture from screen' to add your own" : "Kliknij 'Zaznacz z ekranu' aby dodać własny",
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
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("cfg.msg.save_patterns_err") + ex.Message, Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshPatternList()
        {
            if (_patternListBox == null) return;
            _patternListBox.Items.Clear();
            foreach (var p in _patterns)
            {
                string status = p.IsActive ? "✓" : "✗";
                string name = (p.Name ?? "—").PadRight(25);
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
                Text = Loc.T("cfg.pat.dlg_title"),
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
                    Text = Loc.T("cfg.pat.dlg_label"),
                    Font = UiFonts.CaptionBold,
                    ForeColor = UiTheme.TextSecondary,
                    Location = new Point(24, 24),
                    AutoSize = true
                };
                var nameBox = MakeTextBox(24, 48, 336);
                var okBtn = RoundedButton.Primary(Loc.T("cfg.pat.dlg_btn"), 120, 42);
                okBtn.Location = new Point(120, 108);
                okBtn.Click += (s2, args) =>
                {
                    if (string.IsNullOrWhiteSpace(nameBox.Text))
                    {
                        MessageBox.Show(Loc.T("cfg.pat.dlg_err_no_name"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        Description = Loc.T("cfg.pat.desc_prefix") + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
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
                if (MessageBox.Show(Loc.T("cfg.pat.del_confirm"), Loc.T("common.confirm"),
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
            else MessageBox.Show(Loc.T("cfg.pat.del_select"), Loc.T("common.info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveAllSettings(object sender, EventArgs e)
        {
            try
            {
                if (_db == null)
                {
                    MessageBox.Show(Loc.T("cfg.msg.no_db"), Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var data = _db.GetData();
                data.Patterns = _patterns;
                if (_patterns.Count > 0)
                    data.NextPatternId = _patterns.Max(p => p.Id) + 1;

                data.Tips = _tips;
                if (_tips.Count > 0)
                    data.NextTipId = _tips.Max(t => t.Id) + 1;

                data.Settings["AutoStart"] = (_autoStartCheck != null && _autoStartCheck.Checked).ToString();
                data.Settings["MinimizeToTray"] = (_trayCheck == null || _trayCheck.Checked).ToString();
                data.Settings["BackupPath"] = _backupPathBox.Text;
                if (_monitorPathBox != null) data.Settings["MonitoredFile"] = _monitorPathBox.Text;
                data.Settings["CheckpointPath"] = _checkpointPathBox.Text;
                data.Settings["CheckpointArgs"] = _checkpointArgsBox.Text;
                data.Settings["PatternThreshold"] = _thresholdBox.Value.ToString();
                data.Settings["SearchInterval"] = _intervalBox.Value.ToString();
                if (_retentionBox != null)
                    data.Settings["BackupRetentionDays"] = _retentionBox.Value.ToString();

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
                MessageBox.Show(Loc.T("cfg.msg.saved"), Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("cfg.msg.save_err") + ex.Message, Loc.T("common.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                Text = Loc.T("sel.hint"),
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
