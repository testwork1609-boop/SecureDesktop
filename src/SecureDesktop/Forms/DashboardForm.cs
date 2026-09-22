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
        private static readonly Color PrimaryColor = Color.FromArgb(45, 165, 90);
        private static readonly Color BgColor = Color.FromArgb(248, 249, 250);
        private static readonly Color SidebarColor = Color.White;
        private static readonly Color TextColor = Color.FromArgb(30, 30, 30);

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
        private Button _backButton;

        public DashboardForm(User user, DatabaseInitializer db, int sessionId)
        {
            _currentUser = user;
            _db = db;
            _sessionId = sessionId;
            _lockService = new ScreenLockService { PasswordVerifier = VerifyCurrentUserPassword };

            // Po odblokowaniu ekranu przywracamy panel główny z minimalizacji
            // (bo przed blokadą chowamy go, żeby nie zasłaniał ekranu).
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
            this.Text = "SecureDesktop - Panel Główny";
            this.Size = new Size(1050, 700);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = BgColor;
            this.ForeColor = TextColor;

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = PrimaryColor
            };

            var logoLabel = new Label
            {
                Text = "🔒 SecureDesktop",
                Font = UiFonts.Segoe16Bold,
                Location = new Point(20, 18),
                AutoSize = true,
                ForeColor = Color.White
            };

            _backButton = new Button
            {
                Text = "◀ Wstecz",
                Location = new Point(230, 16),
                Size = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(35, 135, 70),
                ForeColor = Color.White,
                Font = UiFonts.Segoe10Bold,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _backButton.FlatAppearance.BorderSize = 0;
            _backButton.Click += (s, e) => ShowHome();

            _viewTitleLabel = new Label
            {
                Text = "Panel główny",
                Font = UiFonts.Segoe14Bold,
                Location = new Point(350, 22),
                AutoSize = true,
                ForeColor = Color.FromArgb(230, 255, 230)
            };

            _userLabel = new Label
            {
                Text = _currentUser.IdentificationNumber + (_currentUser.IsAdmin ? " (Admin)" : " (User)"),
                Font = UiFonts.Segoe10,
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 255, 220),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            headerPanel.Controls.Add(logoLabel);
            headerPanel.Controls.Add(_backButton);
            headerPanel.Controls.Add(_viewTitleLabel);
            headerPanel.Controls.Add(_userLabel);
            headerPanel.Resize += (s, e) =>
                _userLabel.Location = new Point(headerPanel.Width - _userLabel.Width - 24, 24);

            var sidebarPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 240,
                BackColor = SidebarColor
            };

            int y = 20;

            var homeBtn = CreateSidebarButton("Panel główny", y);
            homeBtn.Click += (s, e) => ShowHome();
            y += 48;

            var sep1 = new Panel { Location = new Point(20, y), Size = new Size(200, 1), BackColor = Color.FromArgb(230, 230, 230) };
            sidebarPanel.Controls.Add(sep1);
            y += 16;

            var lockAllBtn = CreateSidebarButton("Blokuj cały ekran", y);
            lockAllBtn.Click += (s, e) => { try { _lockService.LockAllScreens(); } catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); } };
            y += 48;

            var lockPatternBtn = CreateSidebarButton("Blokuj z Pattern", y);
            lockPatternBtn.Click += OnLockWithPatterns;
            y += 48;

            var checkpointBtn = CreateSidebarButton("CheckPoint", y);
            checkpointBtn.Click += OnCheckpoint;
            y += 48;

            Button configBtn = null;
            Button historyBtn = null;
            Button backupBtn = null;

            if (_currentUser.IsAdmin)
            {
                var sep2 = new Panel { Location = new Point(20, y), Size = new Size(200, 1), BackColor = Color.FromArgb(230, 230, 230) };
                sidebarPanel.Controls.Add(sep2);
                y += 16;

                configBtn = CreateSidebarButton("Konfiguracja", y);
                configBtn.Click += (s, e) =>
                {
                    var view = new ConfigurationView(_db);
                    view.CloseRequested += () => BeginInvoke(new Action(() => { LoadStats(); ShowHome(); }));
                    view.DataSaved += () => BeginInvoke(new Action(LoadStats));
                    ShowView(view, "Konfiguracja");
                };
                y += 48;

                historyBtn = CreateSidebarButton("Historia zdarzeń", y);
                historyBtn.Click += (s, e) =>
                {
                    var view = new EventHistoryView(_db);
                    view.CloseRequested += () => BeginInvoke(new Action(ShowHome));
                    ShowView(view, "Historia zdarzeń");
                };
                y += 48;

                backupBtn = CreateSidebarButton("Wykonaj backup teraz", y);
                backupBtn.Click += OnBackupNow;
                y += 48;
            }

            sidebarPanel.Controls.Add(homeBtn);
            sidebarPanel.Controls.Add(lockAllBtn);
            sidebarPanel.Controls.Add(lockPatternBtn);
            sidebarPanel.Controls.Add(checkpointBtn);
            if (configBtn != null) sidebarPanel.Controls.Add(configBtn);
            if (historyBtn != null) sidebarPanel.Controls.Add(historyBtn);
            if (backupBtn != null) sidebarPanel.Controls.Add(backupBtn);

            var logoutBtn = CreateSidebarButton("Wyloguj", 0);
            logoutBtn.Dock = DockStyle.Bottom;
            logoutBtn.Height = 46;
            logoutBtn.ForeColor = Color.FromArgb(220, 80, 80);
            logoutBtn.Click += OnLogout;
            sidebarPanel.Controls.Add(logoutBtn);

            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgColor,
                Padding = new Padding(16)
            };

            this.Controls.Add(_contentHost);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(headerPanel);
        }

        private Button CreateSidebarButton(string text, int yPos)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(10, yPos),
                Size = new Size(220, 42),
                Font = UiFonts.Segoe11,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(50, 50, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => { btn.BackColor = Color.FromArgb(240, 255, 245); btn.ForeColor = PrimaryColor; };
            btn.MouseLeave += (s, e) => { btn.BackColor = Color.White; btn.ForeColor = Color.FromArgb(50, 50, 50); };
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
            _backButton.Visible = true;
        }

        private void ShowHome()
        {
            LoadStats();
            var home = new DashboardHomeView(_currentUser, _totalPatterns, _activePatterns, _totalEvents);
            ShowView(home, "Panel główny");
            _backButton.Visible = false;
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

                // Zminimalizuj okno Dashboardu, żeby nie zasłaniało ekranu.
                // Bez tego pattern service robi screenshot z widocznym oknem
                // aplikacji i nie znajduje wzorca nagranego z pulpitu pod spodem.
                this.WindowState = FormWindowState.Minimized;

                // Odczekaj ~900 ms - system zdąży odświeżyć pulpit pod spodem,
                // zanim ruszy overlay i pierwszy skan wzorca.
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
        private static readonly Color PrimaryColor = Color.FromArgb(45, 165, 90);

        public DashboardHomeView(User user, int totalPatterns, int activePatterns, int totalEvents)
        {
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Dock = DockStyle.Fill;

            var welcomeCard = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(700, 110),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var welcomeTitle = new Label
            {
                Text = "Witaj, " + user.IdentificationNumber + "!",
                Font = UiFonts.Segoe18Bold,
                Location = new Point(24, 20),
                AutoSize = true,
                ForeColor = PrimaryColor
            };

            var welcomeSubtitle = new Label
            {
                Text = "Zalogowano: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Font = UiFonts.Segoe10,
                Location = new Point(24, 60),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            welcomeCard.Controls.Add(welcomeTitle);
            welcomeCard.Controls.Add(welcomeSubtitle);
            this.Controls.Add(welcomeCard);

            if (user.IsAdmin)
            {
                var statsCard = new Panel
                {
                    Location = new Point(20, 150),
                    Size = new Size(700, 180),
                    BackColor = Color.White,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var title = new Label
                {
                    Text = "Statystyki",
                    Font = UiFonts.Segoe14Bold,
                    Location = new Point(24, 18),
                    AutoSize = true
                };

                var txt = new Label
                {
                    Text = "Wzorców: " + totalPatterns + " (aktywne: " + activePatterns + ")\n" +
                           "Zdarzeń w bazie: " + totalEvents,
                    Font = UiFonts.Segoe11,
                    Location = new Point(24, 60),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(100, 100, 100)
                };

                statsCard.Controls.Add(title);
                statsCard.Controls.Add(txt);
                this.Controls.Add(statsCard);
            }

            var tipCard = new Panel
            {
                Location = new Point(20, user.IsAdmin ? 350 : 150),
                Size = new Size(700, 140),
                BackColor = Color.FromArgb(240, 250, 245),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var tipTitle = new Label
            {
                Text = "💡 Wskazówka",
                Font = UiFonts.Segoe12Bold,
                Location = new Point(24, 16),
                AutoSize = true,
                ForeColor = PrimaryColor
            };
            var tipBody = new Label
            {
                Text = "Wzorce do blokady nagrywaj jako fragmenty, które są ZAWSZE widoczne na ekranie\n" +
                       "(np. ikona w pasku zadań, logo w oknie). Unikaj ikon pulpitu, które bywają zasłonięte.\n" +
                       "Przed użyciem wzorca — sprawdź go przyciskiem „Testuj wzorzec” w Konfiguracji.",
                Font = UiFonts.Segoe10,
                Location = new Point(24, 50),
                Size = new Size(640, 80),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            tipCard.Controls.Add(tipTitle);
            tipCard.Controls.Add(tipBody);
            this.Controls.Add(tipCard);
        }
    }
}
