using System;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private readonly User _currentUser;
        private readonly DatabaseInitializer _db;
        private readonly ScreenLockService _lockService;
        private readonly int _sessionId;

        private int _totalPatterns = 0;
        private int _activePatterns = 0;
        private int _totalEvents = 0;

        private Panel _contentHost;
        private Label _viewTitleLabel;
        private Label _userLabel;
        private RoundedButton _backButton;

        public DashboardForm(User user, DatabaseInitializer db, int sessionId)
        {
            _currentUser = user;
            _db = db;
            _sessionId = sessionId;
            _lockService = new ScreenLockService { PasswordVerifier = VerifyCurrentUserPassword };

            _lockService.LockDeactivated += (s, e) =>
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (this.IsDisposed) return;
                        this.WindowState = FormWindowState.Normal;
                        this.Show();
                        this.Activate();
                    }));
                }
                catch { }
            };

            LoadStats();
            InitializeComponent();
            ShowHome();
        }

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

        private bool VerifyCurrentUserPassword(string password)
        {
            try
            {
                if (string.IsNullOrEmpty(password)) return false;
                var data = _db.GetData();
                if (data == null || data.Users == null) return false;
                var user = data.Users.FirstOrDefault(u => u.Id == _currentUser.Id);
                if (user == null || string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.Salt))
                    return false;
                var hash = SecurityHelper.HashPassword(password, user.Salt);
                return string.Equals(hash, user.PasswordHash, StringComparison.Ordinal);
            }
            catch { return false; }
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
                    string thStr;
                    if (data.Settings.TryGetValue("PatternThreshold", out thStr))
                    {
                        int th;
                        if (int.TryParse(thStr, out th) && th >= 1 && th <= 100)
                            defaultThreshold = th / 100.0;
                    }
                    string ivStr;
                    if (data.Settings.TryGetValue("SearchInterval", out ivStr))
                    {
                        int iv;
                        if (int.TryParse(ivStr, out iv) && iv > 0)
                            intervalMs = iv;
                    }
                }
            }
            catch { }
            return new PatternRecognitionService(defaultThreshold, 0.93, intervalMs);
        }

        private void InitializeComponent()
        {
            this.Icon = Program.AppIcon;
            this.Text = "SecureDesktop — Panel główny";
            this.Size = new Size(1100, 720);
            this.MinimumSize = new Size(950, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = UiTheme.Bg;
            this.ForeColor = UiTheme.TextPrimary;
            this.DoubleBuffered = true;

            // === Header ===
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = UiTheme.Surface
            };
            headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(UiTheme.Border, 1))
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var logoMark = new Panel { Location = new Point(24, 20), Size = new Size(32, 32), BackColor = Color.Transparent };
            logoMark.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(UiTheme.Primary))
                    e.Graphics.FillEllipse(brush, 0, 0, 32, 32);
                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillRectangle(brush, 10, 16, 12, 10);
                    using (var pen = new Pen(Color.White, 2))
                        e.Graphics.DrawArc(pen, 11, 8, 10, 11, 180, 180);
                }
            };
            headerPanel.Controls.Add(logoMark);

            var logoLabel = new Label
            {
                Text = "SecureDesktop",
                Font = UiFonts.H2,
                Location = new Point(66, 24),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary
            };
            headerPanel.Controls.Add(logoLabel);

            _viewTitleLabel = new Label
            {
                Text = "Panel główny",
                Font = UiFonts.Body,
                Location = new Point(220, 28),
                AutoSize = true,
                ForeColor = UiTheme.TextMuted
            };
            headerPanel.Controls.Add(_viewTitleLabel);

            _userLabel = new Label
            {
                Text = _currentUser.IdentificationNumber + (_currentUser.IsAdmin ? "  •  Admin" : "  •  User"),
                Font = UiFonts.Body,
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            headerPanel.Controls.Add(_userLabel);
            headerPanel.Resize += (s, e) =>
                _userLabel.Location = new Point(headerPanel.Width - _userLabel.Width - 32, 28);

            // === Sidebar ===
            var sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = UiTheme.Surface
            };
            sidebarPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(UiTheme.Border, 1))
                    e.Graphics.DrawLine(pen, sidebarPanel.Width - 1, 0, sidebarPanel.Width - 1, sidebarPanel.Height);
            };

            int y = 20;

            var homeBtn = CreateSidebarButton("🏠", "Panel główny", y);
            homeBtn.Click += (s, e) => ShowHome();
            y += 44;

            var lockAllBtn = CreateSidebarButton("🔒", "Blokuj cały ekran", y);
            lockAllBtn.Click += (s, e) => { try { _lockService.LockAllScreens(); } catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); } };
            y += 44;

            var lockPatternBtn = CreateSidebarButton("🎯", "Blokuj z patternem", y);
            lockPatternBtn.Click += OnLockWithPatterns;
            y += 44;

            var checkpointBtn = CreateSidebarButton("⚡", "CheckPoint", y);
            checkpointBtn.Click += OnCheckpoint;
            y += 56;

            sidebarPanel.Controls.Add(homeBtn);
            sidebarPanel.Controls.Add(lockAllBtn);
            sidebarPanel.Controls.Add(lockPatternBtn);
            sidebarPanel.Controls.Add(checkpointBtn);

            if (_currentUser.IsAdmin)
            {
                var sectionLabel = new Label
                {
                    Text = "ADMINISTRACJA",
                    Font = UiFonts.SmallBold,
                    ForeColor = UiTheme.TextMuted,
                    Location = new Point(24, y),
                    AutoSize = true
                };
                sidebarPanel.Controls.Add(sectionLabel);
                y += 24;

                var configBtn = CreateSidebarButton("⚙️", "Konfiguracja", y);
                configBtn.Click += (s, e) =>
                {
                    var view = new ConfigurationView(_db);
                    view.CloseRequested += () => BeginInvoke(new Action(() => { LoadStats(); ShowHome(); }));
                    view.DataSaved += () => BeginInvoke(new Action(LoadStats));
                    ShowView(view, "Konfiguracja");
                };
                sidebarPanel.Controls.Add(configBtn);
                y += 44;

                var historyBtn = CreateSidebarButton("📋", "Historia zdarzeń", y);
                historyBtn.Click += (s, e) =>
                {
                    var view = new EventHistoryView(_db);
                    view.CloseRequested += () => BeginInvoke(new Action(ShowHome));
                    ShowView(view, "Historia zdarzeń");
                };
                sidebarPanel.Controls.Add(historyBtn);
                y += 44;

                var backupBtn = CreateSidebarButton("💾", "Wykonaj backup", y);
                backupBtn.Click += OnBackupNow;
                sidebarPanel.Controls.Add(backupBtn);
            }

            var logoutBtn = new RoundedButton
            {
                Text = "🚪   Wyloguj",
                Dock = DockStyle.Bottom,
                Height = 46,
                CornerRadius = 8,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.DangerLight,
                PressedColor = UiTheme.DangerLight,
                ForeColor = UiTheme.Danger,
                Font = UiFonts.BodyBold,
                Margin = new Padding(16)
            };
            logoutBtn.Click += OnLogout;
            var logoutHost = new Panel { Dock = DockStyle.Bottom, Height = 62, BackColor = UiTheme.Surface, Padding = new Padding(16, 8, 16, 8) };
            logoutHost.Controls.Add(logoutBtn);
            sidebarPanel.Controls.Add(logoutHost);

            // === Content ===
            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Bg,
                Padding = new Padding(28)
            };

            this.Controls.Add(_contentHost);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(headerPanel);
        }

        private RoundedButton CreateSidebarButton(string icon, string text, int yPos)
        {
            var btn = new RoundedButton
            {
                Text = icon + "   " + text,
                Location = new Point(16, yPos),
                Size = new Size(218, 40),
                CornerRadius = 8,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.PrimaryLight,
                PressedColor = UiTheme.PrimaryLight,
                ForeColor = UiTheme.TextPrimary,
                Font = UiFonts.Body,
                TextAlign = ContentAlignment.MiddleLeft
            };
            return btn;
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
            var home = new DashboardHomeView(_currentUser, _totalPatterns, _activePatterns, _totalEvents);
            ShowView(home, "Panel główny");
        }

        private async void OnLockWithPatterns(object sender, EventArgs e)
        {
            try
            {
                var data = _db.GetData();
                var patterns = data == null || data.Patterns == null
                    ? new System.Collections.Generic.List<Pattern>()
                    : data.Patterns.ToList();

                var usable = patterns.Where(p => p.IsActive && p.ImageData != null && p.ImageData.Length > 0).ToList();

                if (usable.Count == 0)
                {
                    MessageBox.Show("Brak używalnych wzorców (aktywnych, z zapisanym obrazem). Dodaj wzorzec w Konfiguracji.",
                        "Pattern Lock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                this.WindowState = FormWindowState.Minimized;
                await System.Threading.Tasks.Task.Delay(900);
                _lockService.LockWithPatterns(usable, CreatePatternRecognitionService());
            }
            catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
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
                    string p;
                    if (data.Settings.TryGetValue("CheckpointPath", out p) && !string.IsNullOrWhiteSpace(p))
                        path = p;
                    string a;
                    if (data.Settings.TryGetValue("CheckpointArgs", out a))
                        args = a == null ? "" : a;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
        }

        private void OnBackupNow(object sender, EventArgs e)
        {
            try
            {
                var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                if (!File.Exists(sourcePath)) { MessageBox.Show("Brak bazy danych."); return; }
                new BackupService().CreateBackup(sourcePath, "Backup");
                MessageBox.Show("Backup utworzony!", "Sukces");
            }
            catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
        }

        private void OnLogout(object sender, EventArgs e)
        {
            try
            {
                new EventLogRepository(_db).Create(new EventLog
                {
                    UserId = _currentUser.Id,
                    IdentificationNumber = _currentUser.IdentificationNumber,
                    OperationName = "Logout",
                    Result = "Success"
                });
                new SessionRepository(_db).EndSession(_sessionId);
            }
            catch { }
            this.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_lockService != null) _lockService.UnlockScreens(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    internal class DashboardHomeView : UserControl
    {
        public DashboardHomeView(User user, int totalPatterns, int activePatterns, int totalEvents)
        {
            this.BackColor = UiTheme.Bg;
            this.Dock = DockStyle.Fill;

            var welcomeCard = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(700, 120),
                BackColor = UiTheme.Bg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            UiTheme.MakeCard(welcomeCard, 12);

            var welcomeTitle = new Label
            {
                Text = "Witaj, " + user.IdentificationNumber + " 👋",
                Font = UiFonts.H1,
                Location = new Point(28, 24),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            };
            var welcomeSubtitle = new Label
            {
                Text = "Zalogowano: " + DateTime.Now.ToString("dddd, d MMMM yyyy — HH:mm"),
                Font = UiFonts.Body,
                Location = new Point(28, 68),
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent
            };
            welcomeCard.Controls.Add(welcomeTitle);
            welcomeCard.Controls.Add(welcomeSubtitle);
            this.Controls.Add(welcomeCard);

            if (user.IsAdmin)
            {
                // Stats - trzy karty w rzędzie
                var statsRow = new Panel
                {
                    Location = new Point(0, 144),
                    Size = new Size(700, 130),
                    BackColor = UiTheme.Bg,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var cardPatterns = CreateStatCard("🎯", totalPatterns.ToString(), "Wzorców", "w tym aktywnych: " + activePatterns);
                var cardEvents = CreateStatCard("📋", totalEvents.ToString(), "Zdarzeń", "w bazie danych");
                var cardStatus = CreateStatCard("✅", "OK", "Status", "system sprawny");

                cardPatterns.Location = new Point(0, 0);
                cardEvents.Location = new Point(244, 0);
                cardStatus.Location = new Point(488, 0);
                cardPatterns.Size = new Size(228, 130);
                cardEvents.Size = new Size(228, 130);
                cardStatus.Size = new Size(228, 130);

                statsRow.Controls.Add(cardPatterns);
                statsRow.Controls.Add(cardEvents);
                statsRow.Controls.Add(cardStatus);
                this.Controls.Add(statsRow);
            }

            var tipCard = new Panel
            {
                Location = new Point(0, user.IsAdmin ? 296 : 144),
                Size = new Size(700, 160),
                BackColor = UiTheme.Bg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            UiTheme.MakeCard(tipCard, 12);

            var tipIcon = new Label
            {
                Text = "💡",
                Font = new Font("Segoe UI", 24),
                Location = new Point(24, 24),
                Size = new Size(40, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            tipCard.Controls.Add(tipIcon);

            var tipTitle = new Label
            {
                Text = "Wskazówka",
                Font = UiFonts.H3,
                Location = new Point(76, 24),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            };
            tipCard.Controls.Add(tipTitle);

            var tipBody = new Label
            {
                Text = "Wzorce nagrywaj z tego, co jest ZAWSZE widoczne na ekranie\n" +
                       "(ikona w pasku zadań, logo w oknie). Unikaj ikon pulpitu, które bywają zasłonięte.\n" +
                       "Przed użyciem sprawdź wzorzec przyciskiem „Testuj wzorzec” w Konfiguracji.",
                Font = UiFonts.Body,
                Location = new Point(76, 58),
                Size = new Size(600, 90),
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent
            };
            tipCard.Controls.Add(tipBody);

            this.Controls.Add(tipCard);
        }

        private Panel CreateStatCard(string icon, string value, string label, string subtext)
        {
            var card = new Panel { BackColor = UiTheme.Bg };
            UiTheme.MakeCard(card, 12);

            var iconLbl = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 18),
                Location = new Point(20, 18),
                Size = new Size(36, 36),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            card.Controls.Add(iconLbl);

            var valueLbl = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                Location = new Point(20, 54),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            };
            card.Controls.Add(valueLbl);

            var labelLbl = new Label
            {
                Text = label,
                Font = UiFonts.Caption,
                Location = new Point(20, 94),
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent
            };
            card.Controls.Add(labelLbl);

            var subLbl = new Label
            {
                Text = subtext,
                Font = UiFonts.Small,
                Location = new Point(20, 110),
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.Transparent
            };
            card.Controls.Add(subLbl);

            return card;
        }
    }
}
