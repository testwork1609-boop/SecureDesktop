using System;
using System.Collections.Generic;
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

        public DashboardForm(User user, DatabaseInitializer db, int sessionId)
        {
            _currentUser = user;
            _db = db;
            _sessionId = sessionId;
            _lockService = new ScreenLockService
            {
                PasswordVerifier = VerifyCurrentUserPassword
            };
            LoadStats();
            InitializeComponent();
        }

        private void LoadStats()
        {
            try
            {
                var data = _db.GetData();
                if (data != null)
                {
                    _totalPatterns = data.Patterns?.Count ?? 0;
                    _activePatterns = data.Patterns?.Count(p => p.IsActive) ?? 0;
                    _totalEvents = data.EventLogs?.Count ?? 0;
                }
            }
            catch { }
        }

        /// <summary>
        /// Weryfikuje hasło aktualnie zalogowanego użytkownika. Wykorzystywane
        /// przez ScreenLockForm przy próbie odblokowania ekranu.
        /// </summary>
        private bool VerifyCurrentUserPassword(string password)
        {
            try
            {
                if (string.IsNullOrEmpty(password)) return false;
                var user = _db.GetData()?.Users?.FirstOrDefault(u => u.Id == _currentUser.Id);
                if (user == null || string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.Salt))
                    return false;

                var hash = SecurityHelper.HashPassword(password, user.Salt);
                return string.Equals(hash, user.PasswordHash, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private PatternRecognitionService CreatePatternRecognitionService()
        {
            double defaultThreshold = 0.75;
            int intervalMs = 200;

            try
            {
                var data = _db.GetData();
                if (data?.Settings != null)
                {
                    if (data.Settings.TryGetValue("PatternThreshold", out var thStr) &&
                        int.TryParse(thStr, out int th) && th >= 1 && th <= 100)
                        defaultThreshold = th / 100.0;

                    if (data.Settings.TryGetValue("SearchInterval", out var ivStr) &&
                        int.TryParse(ivStr, out int iv) && iv > 0)
                        intervalMs = iv;
                }
            }
            catch { }

            return new PatternRecognitionService(defaultThreshold, 0.93, intervalMs);
        }

        private void InitializeComponent()
        {
            this.Icon = Program.AppIcon;
            Color primaryColor = Color.FromArgb(45, 165, 90);
            Color bgColor = Color.FromArgb(248, 249, 250);
            Color sidebarColor = Color.White;
            Color textColor = Color.FromArgb(30, 30, 30);
            Color subtitleColor = Color.FromArgb(100, 100, 100);

            this.Text = "SecureDesktop - Panel Główny";
            this.Size = new Size(850, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(850, 60),
                BackColor = primaryColor
            };

            var logoLabel = new Label
            {
                Text = "SecureDesktop",
                Font = UiFonts.Segoe16Bold,
                Location = new Point(20, 15),
                AutoSize = true,
                ForeColor = Color.White
            };

            var userLabel = new Label
            {
                Text = _currentUser.IdentificationNumber + (_currentUser.IsAdmin ? " (Admin)" : " (User)"),
                Font = UiFonts.Segoe10,
                Location = new Point(620, 22),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 255, 220)
            };

            headerPanel.Controls.Add(logoLabel);
            headerPanel.Controls.Add(userLabel);

            var sidebarPanel = new Panel
            {
                Location = new Point(0, 60),
                Size = new Size(280, 540),
                BackColor = sidebarColor
            };

            var shadowLine = new Panel
            {
                Location = new Point(280, 60),
                Size = new Size(1, 540),
                BackColor = Color.FromArgb(220, 220, 220)
            };

            int yPos = 25;

            var lockAllBtn = CreateSidebarButton("Blokuj cały ekran", yPos, primaryColor);
            lockAllBtn.Click += (s, e) => { try { _lockService.LockAllScreens(); } catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); } };
            yPos += 55;

            var lockPatternBtn = CreateSidebarButton("Blokuj z Pattern", yPos, primaryColor);
            lockPatternBtn.Click += (s, e) =>
            {
                try
                {
                    var patterns = _db.GetData()?.Patterns?.ToList() ?? new List<Pattern>();
                    var usablePatterns = patterns
                        .Where(p => p.IsActive && p.ImageData != null && p.ImageData.Length > 0)
                        .ToList();

                    if (usablePatterns.Count == 0)
                    {
                        var result = MessageBox.Show(
                            "Brak używalnych wzorców (aktywnych, z zapisanym obrazem). Przejść do konfiguracji?",
                            "Pattern Lock", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (result == DialogResult.Yes && _currentUser.IsAdmin)
                        {
                            using (var cfg = new ConfigurationForm(_db))
                                cfg.ShowDialog(this);
                            LoadStats();
                        }
                    }
                    else
                    {
                        _lockService.LockWithPatterns(usablePatterns, CreatePatternRecognitionService());
                    }
                }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
            };
            yPos += 55;

            var checkpointBtn = CreateSidebarButton("CheckPoint", yPos, primaryColor);
            checkpointBtn.Click += (s, e) =>
            {
                try
                {
                    string path = "notepad.exe";
                    string args = "";
                    var data = _db.GetData();
                    if (data?.Settings != null)
                    {
                        if (data.Settings.TryGetValue("CheckpointPath", out var p) && !string.IsNullOrWhiteSpace(p))
                            path = p;
                        if (data.Settings.TryGetValue("CheckpointArgs", out var a))
                            args = a ?? "";
                    }

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = args,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
            };
            yPos += 55;

            Button configBtn = null, historyBtn = null, backupBtn = null;

            if (_currentUser.IsAdmin)
            {
                configBtn = CreateSidebarButton("Konfiguracja", yPos, primaryColor);
                configBtn.Click += (s, e) =>
                {
                    using (var cfg = new ConfigurationForm(_db))
                        cfg.ShowDialog(this);
                    LoadStats();
                };
                yPos += 55;

                historyBtn = CreateSidebarButton("Historia zdarzeń", yPos, primaryColor);
                historyBtn.Click += (s, e) =>
                {
                    using (var hist = new EventHistoryForm(_db))
                        hist.ShowDialog(this);
                };
                yPos += 55;

                backupBtn = CreateSidebarButton("Wykonaj backup", yPos, primaryColor);
                backupBtn.Click += (s, e) =>
                {
                    try
                    {
                        var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                        if (File.Exists(sourcePath))
                        {
                            new BackupService().CreateBackup(sourcePath, "Backup");
                            MessageBox.Show("Backup utworzony!", "Sukces");
                        }
                        else MessageBox.Show("Brak bazy danych.");
                    }
                    catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
                };
                yPos += 55;
            }

            var logoutBtn = CreateSidebarButton("Wyloguj", yPos, Color.FromArgb(220, 80, 80));
            logoutBtn.Click += (s, e) =>
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
            };

            var buttons = new List<Control> { lockAllBtn, lockPatternBtn, checkpointBtn };
            if (configBtn != null) buttons.Add(configBtn);
            if (historyBtn != null) buttons.Add(historyBtn);
            if (backupBtn != null) buttons.Add(backupBtn);
            buttons.Add(logoutBtn);
            sidebarPanel.Controls.AddRange(buttons.ToArray());

            var mainPanel = new Panel
            {
                Location = new Point(300, 80),
                Size = new Size(530, 500),
                BackColor = bgColor
            };

            var welcomeCard = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(490, 100),
                BackColor = Color.White
            };

            var welcomeTitle = new Label
            {
                Text = "Witaj, " + _currentUser.IdentificationNumber + "!",
                Font = UiFonts.Segoe18Bold,
                Location = new Point(20, 20),
                AutoSize = true,
                ForeColor = primaryColor
            };

            var welcomeSubtitle = new Label
            {
                Text = "Zalogowano: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Font = UiFonts.Segoe10,
                Location = new Point(20, 55),
                AutoSize = true,
                ForeColor = subtitleColor
            };

            welcomeCard.Controls.Add(welcomeTitle);
            welcomeCard.Controls.Add(welcomeSubtitle);
            mainPanel.Controls.Add(welcomeCard);

            if (_currentUser.IsAdmin)
            {
                var statsCard = new Panel
                {
                    Location = new Point(20, 140),
                    Size = new Size(490, 150),
                    BackColor = Color.White
                };

                var statsTitle = new Label
                {
                    Text = "Statystyki",
                    Font = UiFonts.Segoe14Bold,
                    Location = new Point(20, 15),
                    AutoSize = true
                };

                var statsText = new Label
                {
                    Text = "Patterny: " + _totalPatterns + " (aktywne: " + _activePatterns + ")\nZdarzenia: " + _totalEvents,
                    Font = UiFonts.Segoe10,
                    Location = new Point(20, 50),
                    AutoSize = true,
                    ForeColor = subtitleColor
                };

                statsCard.Controls.Add(statsTitle);
                statsCard.Controls.Add(statsText);
                mainPanel.Controls.Add(statsCard);
            }

            this.Controls.Add(headerPanel);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(shadowLine);
            this.Controls.Add(mainPanel);
        }

        private Button CreateSidebarButton(string text, int yPos, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(15, yPos),
                Size = new Size(250, 42),
                Font = UiFonts.Segoe11,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(50, 50, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => { btn.BackColor = Color.FromArgb(240, 255, 245); btn.ForeColor = color; };
            btn.MouseLeave += (s, e) => { btn.BackColor = Color.White; btn.ForeColor = Color.FromArgb(50, 50, 50); };
            return btn;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _lockService?.UnlockScreens(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
