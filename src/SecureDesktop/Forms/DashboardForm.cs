using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;
using SecureDesktop.Services;
using SecureDesktop.Utils;

namespace SecureDesktop.Forms
{
    public class DashboardForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        private User _currentUser;
        private int _sessionId;
        private readonly DatabaseInitializer _db;
        private readonly ScreenLockService _lockService;
        private readonly DateTime _sessionStartTime;
        private readonly bool _isFirstRun;

        private int _totalPatterns = 0;
        private int _activePatterns = 0;
        private int _totalEvents = 0;

        private Panel _contentHost;
        private Label _viewTitleLabel;
        private Label _userLabel;
        private bool _lockInProgress;

        // Tray
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _isInTray;

        public DashboardForm(User user, DatabaseInitializer db, int sessionId, bool isFirstRun = false)
        {
            _currentUser = user;
            _db = db;
            _sessionId = sessionId;
            _sessionStartTime = DateTime.Now;
            _isFirstRun = isFirstRun;

            _lockService = new ScreenLockService { CredentialsVerifier = VerifyAnyUserCredentials };

            _lockService.LockDeactivated += (s, e) =>
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (this.IsDisposed) return;
                        RestoreFromTray();
                    }));
                }
                catch { }
            };

            _lockService.UnlockedByUser += OnScreenUnlockedByUser;

            LoadStats();
            InitializeComponent();
            CreateTrayIcon();
            ShowHome();

            if (_isFirstRun)
            {
                BeginInvoke(new Action(ShowFirstRunConfiguration));
            }
        }

        // ============== PIERWSZA KONFIGURACJA ==============

        private void ShowFirstRunConfiguration()
        {
            try
            {
                var view = new ConfigurationView(_db, _currentUser);
                view.CloseRequested += () => BeginInvoke(new Action(() =>
                {
                    if (_isFirstRun) MarkFirstRunCompleted();
                    LoadStats();
                    ShowHome();
                }));
                view.DataSaved += () => BeginInvoke(new Action(() =>
                {
                    // Po pierwszym udanym zapisie ustawień oznacz pierwszą
                    // konfigurację jako zakończoną - żeby przy następnym
                    // uruchomieniu NIE wyskakiwał ponownie komunikat.
                    if (_isFirstRun) MarkFirstRunCompleted();
                    LoadStats();
                }));
                ShowView(view, Loc.T("cfg.title"));
            }
            catch { }
        }

        private void MarkFirstRunCompleted()
        {
            try
            {
                var data = _db.GetData();
                if (data != null && data.Settings != null)
                {
                    data.Settings["FirstRunCompleted"] = "true";
                    _db.Save();
                }
            }
            catch { }
        }

        // ============== TRAY ==============

        private void CreateTrayIcon()
        {
            try
            {
                _trayMenu = new ContextMenuStrip();
                _trayMenu.Items.Add(Loc.T("tray.exit"), null, (s, e) => OnTrayExit());

                _trayIcon = new NotifyIcon
                {
                    Icon = Program.AppIcon,
                    Text = Loc.T("tray.locked"),
                    Visible = false,
                    ContextMenuStrip = _trayMenu
                };

                // Dwuklik na ikonkę w trayu - przywróć okno (jeśli nie zablokowane).
                _trayIcon.DoubleClick += (s, e) =>
                {
                    if (!_lockService.IsLocked) RestoreFromTray();
                };
            }
            catch { }
        }

        private void HideToTray()
        {
            try
            {
                _isInTray = true;
                if (_trayIcon != null)
                {
                    _trayIcon.Text = Loc.T("tray.locked");
                    _trayIcon.Visible = true;
                }
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
            }
            catch { }
        }

        private void RestoreFromTray()
        {
            try
            {
                _isInTray = false;
                if (_trayIcon != null)
                    _trayIcon.Visible = false;

                this.ShowInTaskbar = true;
                this.WindowState = FormWindowState.Normal;
                this.Show();

                // Win32 - niezawodne przywrócenie okna z paska zadań
                // po Hide(). Samo .Show()/.Activate() czasem nie działa.
                try
                {
                    if (this.IsHandleCreated)
                    {
                        ShowWindow(this.Handle, SW_RESTORE);
                        SetForegroundWindow(this.Handle);
                    }
                }
                catch { }

                this.Activate();
                this.BringToFront();
            }
            catch { }
        }

        private void OnTrayExit()
        {
            try
            {
                var result = MessageBox.Show(
                    Loc.T("tray.exit_confirm"),
                    Loc.T("common.confirm"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try { _lockService.UnlockScreens(); } catch { }
                    Application.Exit();
                }
            }
            catch { }
        }

        // ============== LOGIKA UŻYTKOWNIKA ==============

        private void LoadStats()
        {
            try
            {
                var data = _db.GetData();
                if (data != null)
                {
                    _totalPatterns = data.Patterns == null ? 0 : data.Patterns.Count;
                    _activePatterns = data.Patterns == null ? 0 : data.Patterns.Count(p => p.IsActive);
                    _totalEvents = data.EventLogs == null ? 0 : data.EventLogs.Count;
                }
            }
            catch { }
        }

        private DateTime? GetLastBackupTime()
        {
            try
            {
                var data = _db.GetData();
                if (data == null || data.EventLogs == null) return null;
                var last = data.EventLogs
                    .Where(e => string.Equals(e.OperationName, "Backup", StringComparison.OrdinalIgnoreCase)
                             && string.Equals(e.Result, "Success", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(e => e.Timestamp)
                    .FirstOrDefault();
                return last == null ? (DateTime?)null : last.Timestamp;
            }
            catch { return null; }
        }

        private bool HasBackupInThisSession()
        {
            try
            {
                var data = _db.GetData();
                if (data == null || data.EventLogs == null) return false;
                return data.EventLogs.Any(e =>
                    e.UserId == _currentUser.Id &&
                    string.Equals(e.OperationName, "Backup", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.Result, "Success", StringComparison.OrdinalIgnoreCase) &&
                    e.Timestamp >= _sessionStartTime);
            }
            catch { return false; }
        }

        private User VerifyAnyUserCredentials(string pin, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pin) || string.IsNullOrEmpty(password)) return null;
                var data = _db.GetData();
                if (data == null || data.Users == null) return null;

                var user = data.Users.FirstOrDefault(u =>
                    u.IsActive &&
                    string.Equals(u.IdentificationNumber, pin.Trim(), StringComparison.Ordinal));
                if (user == null) return null;

                if (user.UseIndividualPassword &&
                    !string.IsNullOrEmpty(user.PasswordHash) &&
                    !string.IsNullOrEmpty(user.Salt))
                {
                    var hashInd = SecurityHelper.HashPassword(password, user.Salt);
                    if (string.Equals(hashInd, user.PasswordHash, StringComparison.Ordinal))
                        return user;
                }

                if (user.ShiftId.HasValue && data.Shifts != null)
                {
                    var shift = data.Shifts.FirstOrDefault(s => s.Id == user.ShiftId.Value);
                    if (shift != null && !string.IsNullOrEmpty(shift.PasswordHash) && !string.IsNullOrEmpty(shift.Salt))
                    {
                        var hash = SecurityHelper.HashPassword(password, shift.Salt);
                        if (string.Equals(hash, shift.PasswordHash, StringComparison.Ordinal))
                            return user;
                    }
                }

                if (user.IsAdmin && data.Settings != null)
                {
                    string legacy;
                    if (data.Settings.TryGetValue("AdminPassword", out legacy) &&
                        !string.IsNullOrEmpty(legacy) &&
                        string.Equals(password, legacy, StringComparison.Ordinal))
                        return user;
                }

                return null;
            }
            catch { return null; }
        }

        private void OnScreenUnlockedByUser(User unlockedBy)
        {
            if (unlockedBy == null) return;
            if (_currentUser != null && unlockedBy.Id == _currentUser.Id) return;

            try
            {
                BeginInvoke(new Action(() => SwitchCurrentUser(unlockedBy)));
            }
            catch { }
        }

        private void SwitchCurrentUser(User newUser)
        {
            try
            {
                if (this.IsDisposed) return;

                try { new SessionRepository(_db).EndSession(_sessionId); } catch { }

                var oldPin = _currentUser != null ? _currentUser.IdentificationNumber : "—";
                _currentUser = newUser;

                try
                {
                    var session = new Session
                    {
                        UserId = newUser.Id,
                        IdentificationNumber = newUser.IdentificationNumber,
                        SessionToken = SecurityHelper.GenerateSessionToken()
                    };
                    var sessionRepo = new SessionRepository(_db);
                    sessionRepo.Create(session);
                    _sessionId = session.Id;
                }
                catch { _sessionId = 0; }

                try
                {
                    new EventLogRepository(_db).Create(new EventLog
                    {
                        UserId = newUser.Id,
                        IdentificationNumber = newUser.IdentificationNumber,
                        OperationName = "UserSwitch",
                        Result = "Success",
                        Severity = "Info",
                        Description = Loc.T("log.user_switched") + " (" + oldPin + " → " + newUser.IdentificationNumber + ")"
                    });
                }
                catch { }

                UpdateUserLabelText();
                LoadStats();
                ShowHome();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SwitchCurrentUser error: " + ex.Message);
            }
        }

        private void UpdateUserLabelText()
        {
            if (_userLabel == null || _currentUser == null) return;
            string roleSuffix = _currentUser.IsHeadAdmin
                ? "  •  " + Loc.T("dash.user_headadmin")
                : (_currentUser.IsAdmin ? "  •  " + Loc.T("dash.user_admin") : "  •  " + Loc.T("dash.user_user"));
            _userLabel.Text = _currentUser.DisplayNameOrPin + roleSuffix;
        }

        private PatternRecognitionService CreatePatternRecognitionService()
        {
            double defaultThreshold = 0.75;
            int intervalMs = 200;
            try
            {
                var data = _db.GetData();
                if (data != null && data.Settings != null)
                {
                    string s; int iv;
                    if (data.Settings.TryGetValue("PatternThreshold", out s) &&
                        int.TryParse(s, out iv) && iv >= 1 && iv <= 100)
                        defaultThreshold = iv / 100.0;
                    if (data.Settings.TryGetValue("SearchInterval", out s) &&
                        int.TryParse(s, out iv) && iv > 0)
                        intervalMs = iv;
                }
            }
            catch { }
            return new PatternRecognitionService(defaultThreshold, 0.93, intervalMs);
        }

        private void InitializeComponent()
        {
            this.Icon = Program.AppIcon;
            this.Text = Loc.T("dash.window_title");
            this.Size = new Size(1120, 740);
            this.MinimumSize = new Size(950, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = UiTheme.Bg;
            this.ForeColor = UiTheme.TextPrimary;
            this.DoubleBuffered = true;

            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.Surface };
            headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(UiTheme.Border, 1))
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var logoMark = new Panel { Location = new Point(24, 20), Size = new Size(32, 32), BackColor = Color.Transparent };
            logoMark.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(UiTheme.Primary)) e.Graphics.FillEllipse(brush, 0, 0, 32, 32);
                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillRectangle(brush, 10, 16, 12, 10);
                    using (var pen = new Pen(Color.White, 2)) e.Graphics.DrawArc(pen, 11, 8, 10, 11, 180, 180);
                }
            };
            headerPanel.Controls.Add(logoMark);

            headerPanel.Controls.Add(new Label
            {
                Text = Loc.T("dash.header_brand"),
                Font = UiFonts.H2,
                Location = new Point(66, 24),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            });

            _viewTitleLabel = new Label
            {
                Text = Loc.T("dash.view_home"),
                Font = UiFonts.Body,
                Location = new Point(220, 28),
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(_viewTitleLabel);

            _userLabel = new Label
            {
                Text = "",
                Font = UiFonts.Body,
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            UpdateUserLabelText();
            headerPanel.Controls.Add(_userLabel);
            headerPanel.Resize += (s, e) => _userLabel.Location = new Point(headerPanel.Width - _userLabel.Width - 32, 28);

            var sidebarPanel = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = UiTheme.Surface };
            sidebarPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(UiTheme.Border, 1))
                    e.Graphics.DrawLine(pen, sidebarPanel.Width - 1, 0, sidebarPanel.Width - 1, sidebarPanel.Height);
            };

            int y = 20;

            var homeBtn = CreateSidebarButton("🏠", Loc.T("dash.sb.home"), y);
            homeBtn.Click += (s, e) => ShowHome();
            y += 48;

            sidebarPanel.Controls.Add(new Panel { Location = new Point(20, y), Size = new Size(210, 1), BackColor = UiTheme.Border });
            y += 16;

            var lockAllBtn = CreateSidebarButton("🔒", Loc.T("dash.sb.lock_all"), y);
            lockAllBtn.Click += OnLockAllScreens;
            y += 48;

            var lockPatternBtn = CreateSidebarButton("🎯", Loc.T("dash.sb.lock_pattern"), y);
            lockPatternBtn.Click += OnLockWithPatterns;
            y += 48;

            var checkpointBtn = CreateSidebarButton("⚡", Loc.T("dash.sb.checkpoint"), y);
            checkpointBtn.Click += OnCheckpoint;
            y += 56;

            var backupBtn = CreateBackupSidebarButton(y);
            backupBtn.Click += OnBackupNow;
            y += 48;

            sidebarPanel.Controls.Add(homeBtn);
            sidebarPanel.Controls.Add(lockAllBtn);
            sidebarPanel.Controls.Add(lockPatternBtn);
            sidebarPanel.Controls.Add(checkpointBtn);
            sidebarPanel.Controls.Add(backupBtn);

            if (_currentUser.IsAdmin)
            {
                sidebarPanel.Controls.Add(new Panel { Location = new Point(20, y), Size = new Size(210, 1), BackColor = UiTheme.Border });
                y += 16;

                sidebarPanel.Controls.Add(new Label
                {
                    Text = Loc.T("dash.sb.section_admin"),
                    Font = UiFonts.SmallBold,
                    ForeColor = UiTheme.TextMuted,
                    Location = new Point(24, y),
                    AutoSize = true,
                    BackColor = Color.Transparent
                });
                y += 24;

                var configBtn = CreateSidebarButton("⚙", Loc.T("dash.sb.config"), y);
                configBtn.Click += (s, e) =>
                {
                    var view = new ConfigurationView(_db, _currentUser);
                    view.CloseRequested += () => BeginInvoke(new Action(() =>
                    {
                        if (_isFirstRun) MarkFirstRunCompleted();
                        LoadStats();
                        ShowHome();
                    }));
                    view.DataSaved += () => BeginInvoke(new Action(() =>
                    {
                        // Po pierwszym udanym zapisie ustawień oznacz pierwszą
                        // konfigurację jako zakończoną - żeby przy następnym
                        // uruchomieniu NIE wyskakiwał ponownie komunikat.
                        if (_isFirstRun) MarkFirstRunCompleted();
                        LoadStats();
                    }));
                    ShowView(view, Loc.T("cfg.title"));
                };
                sidebarPanel.Controls.Add(configBtn);
                y += 48;

                var historyBtn = CreateSidebarButton("📋", Loc.T("dash.sb.history"), y);
                historyBtn.Click += (s, e) =>
                {
                    var view = new EventHistoryView(_db, _currentUser);
                    view.CloseRequested += () => BeginInvoke(new Action(ShowHome));
                    ShowView(view, Loc.T("hist.title"));
                };
                sidebarPanel.Controls.Add(historyBtn);
            }

            var logoutHost = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = UiTheme.Surface, Padding = new Padding(16, 10, 16, 10) };
            var logoutBtn = new RoundedButton
            {
                Text = "🚪   " + Loc.T("dash.sb.logout"),
                Dock = DockStyle.Fill,
                CornerRadius = 8,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.DangerSoft,
                PressedColor = UiTheme.DangerSoftHover,
                ForeColor = UiTheme.DangerSoftText,
                Font = UiFonts.BodyBold,
                TextAlign = ContentAlignment.MiddleLeft,
                ButtonPadding = new Padding(14, 0, 8, 0)
            };
            logoutBtn.Click += OnLogout;
            logoutHost.Controls.Add(logoutBtn);
            sidebarPanel.Controls.Add(logoutHost);

            _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Bg, Padding = new Padding(28) };

            this.Controls.Add(_contentHost);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(headerPanel);
        }

        private RoundedButton CreateSidebarButton(string icon, string text, int yPos)
        {
            return new RoundedButton
            {
                Text = icon + "   " + text,
                Location = new Point(16, yPos),
                Size = new Size(218, 44),
                CornerRadius = 8,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.PrimarySoftHover,
                PressedColor = UiTheme.PrimarySoftPressed,
                ForeColor = UiTheme.TextPrimary,
                Font = UiFonts.Body,
                TextAlign = ContentAlignment.MiddleLeft,
                ButtonPadding = new Padding(14, 0, 8, 0)
            };
        }

        private RoundedButton CreateBackupSidebarButton(int yPos)
        {
            var amberSoft = Color.FromArgb(254, 243, 199);
            var amberSoftHover = Color.FromArgb(253, 230, 138);
            var amberPressed = Color.FromArgb(252, 211, 77);
            var amberText = Color.FromArgb(180, 83, 9);

            bool highlight = !_currentUser.IsAdmin;

            return new RoundedButton
            {
                Text = "💾   " + Loc.T("dash.sb.backup"),
                Location = new Point(16, yPos),
                Size = new Size(218, 44),
                CornerRadius = 8,
                NormalColor = highlight ? amberSoft : UiTheme.Surface,
                HoverColor = highlight ? amberSoftHover : UiTheme.SurfaceAlt,
                PressedColor = highlight ? amberPressed : UiTheme.Border,
                ForeColor = highlight ? amberText : UiTheme.TextSecondary,
                Font = highlight ? UiFonts.BodyBold : UiFonts.Body,
                TextAlign = ContentAlignment.MiddleLeft,
                ButtonPadding = new Padding(14, 0, 8, 0)
            };
        }

        private void ShowView(UserControl view, string title)
        {
            _contentHost.SuspendLayout();
            Control old = _contentHost.Controls.Count > 0 ? _contentHost.Controls[0] : null;
            _contentHost.Controls.Clear();
            if (old != null) { try { old.Dispose(); } catch { } }
            view.Dock = DockStyle.Fill;
            _contentHost.Controls.Add(view);
            _contentHost.ResumeLayout();
            _viewTitleLabel.Text = title;
        }

        private void ShowHome()
        {
            LoadStats();
            List<Tip> tips;
            try
            {
                var data = _db.GetData();
                tips = data != null && data.Tips != null
                    ? data.Tips.Where(t => t.IsActive).OrderBy(t => t.Id).ToList()
                    : new List<Tip>();
            }
            catch { tips = new List<Tip>(); }

            var home = new DashboardHomeView(_currentUser, _totalPatterns, _activePatterns, _totalEvents,
                tips, GetLastBackupTime(), OnBackupNow);
            ShowView(home, Loc.T("dash.view_home"));
        }

        private async void OnLockAllScreens(object sender, EventArgs e)
        {
            if (_lockInProgress) return;

            // Pytanie o potwierdzenie
            bool confirmed = WarningDialog.Confirm(
                this,
                Loc.T("lock.confirm_all_title"),
                Loc.T("lock.confirm_all_msg"),
                Loc.T("lock.confirm_yes"),
                Loc.T("lock.confirm_no"));

            if (!confirmed) return;

            _lockInProgress = true;
            try
            {
                HideToTray();
                await System.Threading.Tasks.Task.Delay(500);
                _lockService.LockAllScreens();
            }
            catch (Exception ex)
            {
                RestoreFromTray();
                MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _lockInProgress = false; }
        }

        private async void OnLockWithPatterns(object sender, EventArgs e)
        {
            if (_lockInProgress) return;

            // Pytanie o potwierdzenie
            bool confirmed = WarningDialog.Confirm(
                this,
                Loc.T("lock.confirm_pattern_title"),
                Loc.T("lock.confirm_pattern_msg"),
                Loc.T("lock.confirm_yes"),
                Loc.T("lock.confirm_no"));

            if (!confirmed) return;

            _lockInProgress = true;
            try
            {
                var data = _db.GetData();
                var patterns = data == null || data.Patterns == null
                    ? new List<Pattern>()
                    : data.Patterns.ToList();

                var usable = patterns.Where(p => p.IsActive && p.ImageData != null && p.ImageData.Length > 0).ToList();

                if (usable.Count == 0)
                {
                    MessageBox.Show(Loc.T("dash.msg.no_patterns"), Loc.T("dash.msg.patternlock_title"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                HideToTray();
                await System.Threading.Tasks.Task.Delay(900);
                _lockService.LockWithPatterns(usable, CreatePatternRecognitionService());
            }
            catch (Exception ex)
            {
                RestoreFromTray();
                MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _lockInProgress = false; }
        }

        private void OnCheckpoint(object sender, EventArgs e)
        {
            try
            {
                string path = "notepad.exe";
                string args = "";
                var data = _db.GetData();
                if (data != null && data.Settings != null)
                {
                    string p, a;
                    if (data.Settings.TryGetValue("CheckpointPath", out p) && !string.IsNullOrWhiteSpace(p)) path = p;
                    if (data.Settings.TryGetValue("CheckpointArgs", out a)) args = a == null ? "" : a;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnBackupNow(object sender, EventArgs e)
        {
            try
            {
                string backupFolder = "Backup";
                try
                {
                    var data = _db.GetData();
                    if (data != null && data.Settings != null)
                    {
                        string bp;
                        if (data.Settings.TryGetValue("BackupPath", out bp) && !string.IsNullOrWhiteSpace(bp))
                            backupFolder = bp;
                    }
                }
                catch { }

                string sourcePath = null;
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Title = Loc.T("dash.backup_dlg_title");
                    dlg.Filter = Loc.IsEnglish ? "All files|*.*" : "Wszystkie pliki|*.*";
                    dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    sourcePath = dlg.FileName;
                }

                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    MessageBox.Show(Loc.T("dash.msg.backup_no_file"), Loc.T("common.info"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var svc = new BackupService();
                string destPath = svc.CreateUserBackup(sourcePath, backupFolder, _currentUser.IdentificationNumber);

                try
                {
                    new EventLogRepository(_db).Create(new EventLog
                    {
                        UserId = _currentUser.Id,
                        IdentificationNumber = _currentUser.IdentificationNumber,
                        OperationName = "Backup",
                        Result = "Success",
                        Severity = "Info",
                        Description = "Backup pliku: " + sourcePath + " -> " + destPath
                    });
                }
                catch { }

                ShowHome();

                MessageBox.Show(
                    Loc.T("dash.msg.backup_done") + "\n\n" +
                    Loc.T("dash.backup_folder_hint") + "\n" + destPath,
                    Loc.T("common.success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnLogout(object sender, EventArgs e)
        {
            try
            {
                if (_isFirstRun) MarkFirstRunCompleted();

                bool backupDone = HasBackupInThisSession();

                new EventLogRepository(_db).Create(new EventLog
                {
                    UserId = _currentUser.Id,
                    IdentificationNumber = _currentUser.IdentificationNumber,
                    OperationName = "Logout",
                    Result = backupDone ? "Success" : "Warning",
                    Severity = backupDone ? "Info" : "Warning",
                    Description = backupDone
                        ? Loc.T("log.session_backup_ok")
                        : Loc.T("log.session_no_backup")
                });

                new SessionRepository(_db).EndSession(_sessionId);
            }
            catch { }
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isFirstRun) MarkFirstRunCompleted();
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Gdy użytkownik kliknie ikonę w pasku zadań, a okno było
        /// ukryte w trayu - przywróć je poprawnie przez Win32.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_RESTORE = 0xF120;

            if (m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt32() & 0xFFF0) == SC_RESTORE)
            {
                if (_isInTray)
                {
                    RestoreFromTray();
                    return;
                }
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.Visible = false;
                        _trayIcon.Dispose();
                        _trayIcon = null;
                    }
                }
                catch { }

                try { _lockService.UnlockedByUser -= OnScreenUnlockedByUser; } catch { }
                try { if (_lockService != null) _lockService.UnlockScreens(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    // ============== HOME VIEW ==============

    internal class DashboardHomeView : UserControl
    {
        private static readonly Color Amber = Color.FromArgb(245, 158, 11);
        private static readonly Color AmberSoft = Color.FromArgb(254, 243, 199);
        private static readonly Color AmberText = Color.FromArgb(146, 64, 14);

        public DashboardHomeView(User user, int totalPatterns, int activePatterns, int totalEvents,
                                 List<Tip> tips, DateTime? lastBackup, EventHandler onBackupClick)
        {
            this.BackColor = UiTheme.Bg;
            this.Dock = DockStyle.Fill;

            var welcomeCard = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(760, 120),
                BackColor = UiTheme.Bg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            UiTheme.MakeCard(welcomeCard, 12);

            welcomeCard.Controls.Add(new Label
            {
                Text = Loc.T("dash.home.greeting", user.DisplayNameOrPin),
                Font = UiFonts.H1,
                Location = new Point(28, 24),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            });
            welcomeCard.Controls.Add(new Label
            {
                Text = Loc.T("dash.home.logged_at", DateTime.Now.ToString("dddd, d MMMM yyyy — HH:mm")),
                Font = UiFonts.Body,
                Location = new Point(28, 68),
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent
            });
            this.Controls.Add(welcomeCard);

            int nextTop = 144;

            if (user.IsAdmin)
            {
                var statsRow = new Panel
                {
                    Location = new Point(0, nextTop),
                    Size = new Size(760, 130),
                    BackColor = UiTheme.Bg,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var c1 = CreateStatCard("🎯", totalPatterns.ToString(), Loc.T("dash.home.stat_patterns"), Loc.T("dash.home.stat_patterns_sub", activePatterns));
                var c2 = CreateStatCard("📋", totalEvents.ToString(), Loc.T("dash.home.stat_events"), Loc.T("dash.home.stat_events_sub"));
                var c3 = CreateStatCard("✅", "OK", Loc.T("dash.home.stat_status"), Loc.T("dash.home.stat_status_sub"));

                c1.Location = new Point(0, 0); c2.Location = new Point(264, 0); c3.Location = new Point(528, 0);
                c1.Size = c2.Size = c3.Size = new Size(248, 130);

                statsRow.Controls.Add(c1); statsRow.Controls.Add(c2); statsRow.Controls.Add(c3);
                this.Controls.Add(statsRow);
                nextTop = 296;
            }
            else
            {
                var backupCard = new Panel
                {
                    Location = new Point(0, nextTop),
                    Size = new Size(760, 210),
                    BackColor = UiTheme.Bg,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                UiTheme.MakeCard(backupCard, 12);
                backupCard.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var path = UiTheme.RoundedPath(new Rectangle(0, 0, backupCard.Width - 1, backupCard.Height - 1), 12))
                    using (var brush = new SolidBrush(AmberSoft))
                        e.Graphics.FillPath(brush, path);
                    using (var path = UiTheme.RoundedPath(new Rectangle(0, 0, backupCard.Width - 1, backupCard.Height - 1), 12))
                    using (var pen = new Pen(Color.FromArgb(252, 211, 77), 1))
                        e.Graphics.DrawPath(pen, path);
                };
                backupCard.Invalidate();

                backupCard.Controls.Add(new Label
                {
                    Text = "💾",
                    Font = new Font("Segoe UI", 28),
                    Location = new Point(24, 20),
                    Size = new Size(56, 56),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                });

                backupCard.Controls.Add(new Label
                {
                    Text = Loc.IsEnglish
                        ? "RUN A BACKUP BEFORE YOU START WORK"
                        : "WYKONAJ BACKUP PRZED ROZPOCZĘCIEM PRACY",
                    Font = UiFonts.H3,
                    Location = new Point(92, 26),
                    AutoSize = true,
                    ForeColor = AmberText,
                    BackColor = Color.Transparent
                });

                backupCard.Controls.Add(new Label
                {
                    Text = Loc.IsEnglish
                        ? "Select the file you want to back up. The copy will be saved\nin the configured backup folder, in a subfolder named with the date and your PIN."
                        : "Wskaż plik, którego kopię chcesz wykonać. Kopia zostanie zapisana\nw folderze backupu z konfiguracji, w podfolderze z datą i Twoim PIN-em.",
                    Font = UiFonts.Body,
                    Location = new Point(92, 60),
                    Size = new Size(640, 60),
                    ForeColor = AmberText,
                    BackColor = Color.Transparent
                });

                string lastBackupText = lastBackup.HasValue
                    ? (Loc.IsEnglish
                        ? "Last backup: " + lastBackup.Value.ToString("yyyy-MM-dd HH:mm")
                        : "Ostatni backup: " + lastBackup.Value.ToString("yyyy-MM-dd HH:mm"))
                    : (Loc.IsEnglish
                        ? "No backups have been made yet."
                        : "Nie wykonano jeszcze żadnego backupu.");

                backupCard.Controls.Add(new Label
                {
                    Text = lastBackupText,
                    Font = UiFonts.CaptionBold,
                    Location = new Point(92, 118),
                    AutoSize = true,
                    ForeColor = lastBackup.HasValue ? UiTheme.PrimarySoftText : UiTheme.Danger,
                    BackColor = Color.Transparent
                });

                var btnBackup = new RoundedButton
                {
                    Text = "💾   " + (Loc.IsEnglish ? "Run backup now" : "Wykonaj backup teraz"),
                    Location = new Point(92, 148),
                    Size = new Size(260, 44),
                    CornerRadius = 10,
                    NormalColor = Amber,
                    HoverColor = Color.FromArgb(217, 119, 6),
                    PressedColor = Color.FromArgb(180, 83, 9),
                    ForeColor = Color.White,
                    Font = UiFonts.BodyBold
                };
                btnBackup.Click += (s, e) => { if (onBackupClick != null) onBackupClick(s, e); };
                backupCard.Controls.Add(btnBackup);

                this.Controls.Add(backupCard);
                nextTop = 372;
            }

            var tipCard = new Panel
            {
                Location = new Point(0, nextTop),
                Size = new Size(760, Math.Max(180, 600 - nextTop)),
                BackColor = UiTheme.Bg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            UiTheme.MakeCard(tipCard, 12);

            tipCard.Controls.Add(new Label
            {
                Text = "💡",
                Font = new Font("Segoe UI", 18),
                Location = new Point(20, 14),
                Size = new Size(40, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });

            tipCard.Controls.Add(new Label
            {
                Text = Loc.T("dash.home.tips"),
                Font = UiFonts.H3,
                Location = new Point(68, 20),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            });

            var scroll = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(tipCard.Width - 40, tipCard.Height - 80),
                BackColor = Color.Transparent,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            tipCard.Controls.Add(scroll);
            PopulateTips(scroll, tips, tipCard.Width - 60);

            tipCard.Resize += (s, e) =>
            {
                scroll.Size = new Size(tipCard.Width - 40, tipCard.Height - 80);
                PopulateTips(scroll, tips, tipCard.Width - 60);
            };

            this.Controls.Add(tipCard);
        }

        private void PopulateTips(Panel container, List<Tip> tips, int width)
        {
            container.SuspendLayout();
            var toRemove = new List<Control>();
            foreach (Control c in container.Controls) toRemove.Add(c);
            foreach (var c in toRemove) { container.Controls.Remove(c); c.Dispose(); }

            if (width < 100) width = 100;

            if (tips == null || tips.Count == 0)
            {
                container.Controls.Add(new Label
                {
                    Text = Loc.T("dash.home.tips_empty"),
                    Font = UiFonts.Body,
                    ForeColor = UiTheme.TextMuted,
                    Location = new Point(0, 0),
                    AutoSize = true,
                    MaximumSize = new Size(width, 0),
                    BackColor = Color.Transparent
                });
                container.ResumeLayout();
                return;
            }

            int y = 0;
            for (int i = 0; i < tips.Count; i++)
            {
                var tip = tips[i];

                var titleLbl = new Label
                {
                    Text = (tip.Title ?? "").ToUpperInvariant(),
                    Font = UiFonts.CaptionBold,
                    ForeColor = UiTheme.PrimarySoftText,
                    Location = new Point(0, y),
                    AutoSize = true,
                    MaximumSize = new Size(width, 0),
                    BackColor = Color.Transparent
                };
                container.Controls.Add(titleLbl);
                y += titleLbl.PreferredHeight + 4;

                var contentLbl = new Label
                {
                    Text = tip.Content ?? "",
                    Font = UiFonts.Body,
                    ForeColor = UiTheme.TextSecondary,
                    Location = new Point(0, y),
                    AutoSize = true,
                    MaximumSize = new Size(width, 0),
                    BackColor = Color.Transparent
                };
                container.Controls.Add(contentLbl);
                y += contentLbl.PreferredHeight + 16;

                if (i < tips.Count - 1)
                {
                    container.Controls.Add(new Panel
                    {
                        Location = new Point(0, y - 8),
                        Size = new Size(width, 1),
                        BackColor = UiTheme.Border
                    });
                }
            }
            container.ResumeLayout();
        }

        private Panel CreateStatCard(string icon, string value, string label, string subtext)
        {
            var card = new Panel { BackColor = UiTheme.Bg };
            UiTheme.MakeCard(card, 12);

            card.Controls.Add(new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 18),
                Location = new Point(20, 18),
                Size = new Size(40, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            card.Controls.Add(new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                Location = new Point(70, 20),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            });
            card.Controls.Add(new Label
            {
                Text = label,
                Font = UiFonts.Caption,
                Location = new Point(20, 74),
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent
            });
            card.Controls.Add(new Label
            {
                Text = subtext,
                Font = UiFonts.Small,
                Location = new Point(20, 94),
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.Transparent
            });

            return card;
        }
    }
}
