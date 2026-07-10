using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Models;
using SecureDesktop.Services;

namespace SecureDesktop.Forms
{
    public class DashboardForm : Form
    {
        private readonly User _currentUser;
        private readonly DatabaseInitializer _db;
        private readonly ScreenLockService _lockService;

        public DashboardForm(User user, DatabaseInitializer db)
        {
            _currentUser = user;
            _db = db;
            _lockService = new ScreenLockService();
            
            InitializeComponent();
        }

        private void InitializeComponent()
        {
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

            // Nagłówek
            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(850, 60),
                BackColor = primaryColor
            };

            var logoLabel = new Label
            {
                Text = "🛡️ SecureDesktop",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true,
                ForeColor = Color.White
            };

            var userLabel = new Label
            {
                Text = $"{_currentUser.IdentificationNumber}" + (_currentUser.IsAdmin ? " (Admin)" : ""),
                Font = new Font("Segoe UI", 10),
                Location = new Point(620, 22),
                AutoSize = true,
                ForeColor = Color.FromArgb(220, 255, 220)
            };

            headerPanel.Controls.Add(logoLabel);
            headerPanel.Controls.Add(userLabel);

            // Panel boczny
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

            // === PRZYCISK: Blokuj cały ekran ===
            var lockAllBtn = CreateSidebarButton("🔒  Blokuj cały ekran", yPos, primaryColor);
            lockAllBtn.Click += (s, e) =>
            {
                try
                {
                    _lockService.LockAllScreens();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd blokowania: " + ex.Message, "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            // === PRZYCISK: Blokuj z Pattern ===
            var lockPatternBtn = CreateSidebarButton("🎯  Blokuj z Pattern", yPos, primaryColor);
            lockPatternBtn.Click += (s, e) =>
            {
                try
                {
                    // POBIERZ PATTERNY BEZPOŚREDNIO Z BAZY
                    var patterns = _db.GetData().Patterns?.Where(p => p.IsActive).ToList() ?? new List<Pattern>();

                    if (patterns.Count == 0)
                    {
                        var result = MessageBox.Show(
                            "Brak aktywnych wzorców (Pattern).\n\n" +
                            "Czy chcesz przejść do konfiguracji i dodać wzorce?",
                            "Pattern Lock - Brak wzorców",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            var configForm = new ConfigurationForm();
                            configForm.ShowDialog(this);
                        }
                    }
                    else
                    {
                        // Uruchom blokadę z patternami
                        var patternService = new PatternRecognitionService(0.90);
                        _lockService.LockWithPatterns(patterns, patternService);

                        // Logowanie
                        var eventRepo = new Database.Repositories.EventLogRepository(_db);
                        eventRepo.Create(new EventLog
                        {
                            UserId = _currentUser.Id,
                            IdentificationNumber = _currentUser.IdentificationNumber,
                            OperationName = "PatternLock",
                            Result = "Success",
                            Description = $"Uruchomiono blokadę z {patterns.Count} wzorcami"
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd Pattern Lock: " + ex.Message, "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            // === PRZYCISK: CheckPoint ===
            var checkpointBtn = CreateSidebarButton("⚡  CheckPoint", yPos, primaryColor);
            checkpointBtn.Click += (s, e) =>
            {
                try
                {
                    var path = _db.GetData().Settings.ContainsKey("CheckpointPath")
                        ? _db.GetData().Settings["CheckpointPath"]
                        : "notepad.exe";
                    var args = _db.GetData().Settings.ContainsKey("CheckpointArgs")
                        ? _db.GetData().Settings["CheckpointArgs"]
                        : "";

                    System.Diagnostics.Process.Start(path, args);

                    var eventRepo = new Database.Repositories.EventLogRepository(_db);
                    eventRepo.Create(new EventLog
                    {
                        UserId = _currentUser.Id,
                        IdentificationNumber = _currentUser.IdentificationNumber,
                        OperationName = "CheckPoint",
                        Result = "Success",
                        Description = $"Uruchomiono: {path} {args}"
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd CheckPoint: " + ex.Message, "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            // === PRZYCISK: Konfiguracja ===
            var configBtn = CreateSidebarButton("⚙️  Konfiguracja", yPos, primaryColor);
            configBtn.Click += (s, e) =>
            {
                var configForm = new ConfigurationForm();
                configForm.ShowDialog(this);
            };
            yPos += 55;

            // === PRZYCISK: Historia ===
            var historyBtn = CreateSidebarButton("📊  Historia zdarzeń", yPos, primaryColor);
            historyBtn.Click += (s, e) =>
            {
                var historyForm = new EventHistoryForm();
                historyForm.ShowDialog(this);
            };
            yPos += 55;

            // === PRZYCISK: Backup ===
            var backupBtn = CreateSidebarButton("💾  Wykonaj backup", yPos, primaryColor);
            backupBtn.Click += (s, e) =>
            {
                try
                {
                    var sourcePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                    if (System.IO.File.Exists(sourcePath))
                    {
                        var backupService = new BackupService();
                        var backupPath = _db.GetData().Settings.ContainsKey("BackupPath")
                            ? _db.GetData().Settings["BackupPath"]
                            : "Backup";
                        var result = backupService.CreateBackup(sourcePath, backupPath);
                        MessageBox.Show("✅ Backup utworzony!\n" + result, "Sukces");
                    }
                    else
                    {
                        MessageBox.Show("Brak pliku bazy danych.", "Info");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd backupu: " + ex.Message, "Błąd");
                }
            };
            yPos += 55;

            // === PRZYCISK: Wyloguj ===
            var logoutBtn = CreateSidebarButton("🚪  Wyloguj", yPos, Color.FromArgb(220, 80, 80));
            logoutBtn.Click += (s, e) =>
            {
                try
                {
                    var eventRepo = new Database.Repositories.EventLogRepository(_db);
                    eventRepo.Create(new EventLog
                    {
                        UserId = _currentUser.Id,
                        IdentificationNumber = _currentUser.IdentificationNumber,
                        OperationName = "Logout",
                        Result = "Success",
                        Description = "Użytkownik wylogowany"
                    });
                }
                catch { }
                this.Close();
            };

            sidebarPanel.Controls.AddRange(new Control[] { lockAllBtn, lockPatternBtn, checkpointBtn, 
                                                           configBtn, historyBtn, backupBtn, logoutBtn });

            // Panel główny
            var mainPanel = new Panel
            {
                Location = new Point(300, 80),
                Size = new Size(530, 500),
                BackColor = bgColor
            };

            // Karta powitalna
            var welcomeCard = new Panel
            {
                Location = new Point(20, 20),
                Size = new Size(490, 150),
                BackColor = Color.White
            };

            var welcomeTitle = new Label
            {
                Text = $"Witaj, {_currentUser.IdentificationNumber}!",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true,
                ForeColor = primaryColor
            };

            var welcomeSubtitle = new Label
            {
                Text = $"Zalogowano: {DateTime.Now:yyyy-MM-dd HH:mm}",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 55),
                AutoSize = true,
                ForeColor = subtitleColor
            };

            // Pokaż liczbę patternów
            var patternCount = _db.GetData().Patterns?.Count(p => p.IsActive) ?? 0;
            var welcomeText = new Label
            {
                Text = $"Aktywne patterny: {patternCount} | Wybierz funkcję z menu.",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 85),
                AutoSize = true,
                ForeColor = subtitleColor
            };

            welcomeCard.Controls.AddRange(new Control[] { welcomeTitle, welcomeSubtitle, welcomeText });

            // Karta statystyk
            var statsCard = new Panel
            {
                Location = new Point(20, 190),
                Size = new Size(490, 150),
                BackColor = Color.White
            };

            var statsTitle = new Label
            {
                Text = "📈  Statystyki",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };

            var totalPatterns = _db.GetData().Patterns?.Count ?? 0;
            var activePatterns = _db.GetData().Patterns?.Count(p => p.IsActive) ?? 0;
            var totalEvents = _db.GetData().EventLogs?.Count ?? 0;

            var statsText = new Label
            {
                Text = $"• Wszystkie patterny: {totalPatterns}\n" +
                       $"• Aktywne patterny: {activePatterns}\n" +
                       $"• Zdarzenia: {totalEvents}",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 50),
                AutoSize = true,
                ForeColor = subtitleColor
            };

            statsCard.Controls.AddRange(new Control[] { statsTitle, statsText });

            mainPanel.Controls.Add(welcomeCard);
            mainPanel.Controls.Add(statsCard);

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
                Font = new Font("Segoe UI", 11),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(50, 50, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) =>
            {
                btn.BackColor = Color.FromArgb(240, 255, 245);
                btn.ForeColor = color;
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.BackColor = Color.White;
                btn.ForeColor = Color.FromArgb(50, 50, 50);
            };

            return btn;
        }
    }
}