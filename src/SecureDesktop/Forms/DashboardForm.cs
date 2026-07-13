using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
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

            // ========== NAGŁÓWEK ==========
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

            // ========== PANEL BOCZNY ==========
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

            // === PRZYCISK 1: Blokuj cały ekran ===
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

            // === PRZYCISK 2: Blokuj z Pattern ===
            var lockPatternBtn = CreateSidebarButton("🎯  Blokuj z Pattern", yPos, primaryColor);
            lockPatternBtn.Click += (s, e) =>
            {
                try
                {
                    // Ścieżka do pliku bazy danych
                    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");

                    // Sprawdź czy plik istnieje
                    if (!File.Exists(dbPath))
                    {
                        MessageBox.Show("Plik bazy danych nie istnieje!\n\n" + dbPath,
                            "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Wczytaj dane z JSON
                    var json = File.ReadAllText(dbPath);
                    var data = JsonConvert.DeserializeObject<DatabaseData>(json);

                    if (data == null)
                    {
                        MessageBox.Show("Nie można odczytać bazy danych.",
                            "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Pobierz aktywne patterny
                    var patterns = data.Patterns?.Where(p => p.IsActive).ToList() ?? new List<Pattern>();

                    // Sprawdź ile jest patternów
                    int totalPatterns = data.Patterns?.Count ?? 0;
                    int activePatterns = patterns.Count;

                    if (activePatterns == 0)
                    {
                        string info = $"W bazie jest {totalPatterns} wzorców (w tym {activePatterns} aktywnych).\n\n" +
                                       "Brak aktywnych wzorców do użycia.\n\n" +
                                       "Czy chcesz przejść do konfiguracji?";

                        var result = MessageBox.Show(info, "Pattern Lock - Brak wzorców",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            var configForm = new ConfigurationForm();
                            configForm.ShowDialog(this);
                        }
                    }
                    else
                    {
                        // Wyświetl nazwy znalezionych wzorców
                        var patternNames = string.Join("\n• ", patterns.Select(p => p.Name));
                        
                        var result = MessageBox.Show(
                            $"✅ Znaleziono {activePatterns} aktywnych wzorców:\n\n• {patternNames}\n\n" +
                            "Czy na pewno uruchomić blokadę Pattern?",
                            "Pattern Lock",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            // Uruchom blokadę z patternami
                            var patternService = new PatternRecognitionService(0.90);
                            _lockService.LockWithPatterns(patterns, patternService);

                            // Logowanie zdarzenia
                            var eventRepo = new Database.Repositories.EventLogRepository(_db);
                            eventRepo.Create(new EventLog
                            {
                                UserId = _currentUser.Id,
                                IdentificationNumber = _currentUser.IdentificationNumber,
                                OperationName = "PatternLock",
                                Result = "Success",
                                Description = $"Uruchomiono blokadę z {activePatterns} wzorcami"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd Pattern Lock: " + ex.Message,
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            // === PRZYCISK 3: CheckPoint ===
            var checkpointBtn = CreateSidebarButton("⚡  CheckPoint", yPos, primaryColor);
            checkpointBtn.Click += (s, e) =>
            {
                try
                {
                    string path = "notepad.exe";
                    string args = "";

                    // Spróbuj odczytać z konfiguracji
                    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                    if (File.Exists(dbPath))
                    {
                        var json = File.ReadAllText(dbPath);
                        var data = JsonConvert.DeserializeObject<DatabaseData>(json);
                        if (data != null && data.Settings != null)
                        {
                            if (data.Settings.ContainsKey("CheckpointPath"))
                                path = data.Settings["CheckpointPath"];
                            if (data.Settings.ContainsKey("CheckpointArgs"))
                                args = data.Settings["CheckpointArgs"];
                        }
                    }

                    System.Diagnostics.Process.Start(path, args);

                    // Logowanie
                    var eventRepo = new Database.Repositories.EventLogRepository(_db);
                    eventRepo.Create(new EventLog
                    {
                        UserId = _currentUser.Id,
                        IdentificationNumber = _currentUser.IdentificationNumber,
                        OperationName = "CheckPoint",
                        Result = "Success",
                        Description = $"Uruchomiono: {path} {args}"
                    });

                    MessageBox.Show($"✅ Uruchomiono: {path}", "CheckPoint",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd CheckPoint: " + ex.Message, "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            // === PRZYCISK 4: Konfiguracja ===
            var configBtn = CreateSidebarButton("⚙️  Konfiguracja", yPos, primaryColor);
            configBtn.Click += (s, e) =>
            {
                var configForm = new ConfigurationForm();
                configForm.ShowDialog(this);

                // Po zamknięciu konfiguracji, odśwież dane
                RefreshStats();
            };
            yPos += 55;

            // === PRZYCISK 5: Historia zdarzeń ===
            var historyBtn = CreateSidebarButton("📊  Historia zdarzeń", yPos, primaryColor);
            historyBtn.Click += (s, e) =>
            {
                var historyForm = new EventHistoryForm();
                historyForm.ShowDialog(this);
            };
            yPos += 55;

            // === PRZYCISK 6: Backup ===
            var backupBtn = CreateSidebarButton("💾  Wykonaj backup", yPos, primaryColor);
            backupBtn.Click += (s, e) =>
            {
                try
                {
                    var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                    if (File.Exists(sourcePath))
                    {
                        var backupService = new BackupService();
                        var backupFolder = "Backup";
                        
                        // Odczytaj folder backupu z konfiguracji
                        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                        if (File.Exists(dbPath))
                        {
                            var json = File.ReadAllText(dbPath);
                            var data = JsonConvert.DeserializeObject<DatabaseData>(json);
                            if (data?.Settings?.ContainsKey("BackupPath") == true)
                                backupFolder = data.Settings["BackupPath"];
                        }
                        
                        var result = backupService.CreateBackup(sourcePath, backupFolder);
                        MessageBox.Show("✅ Backup utworzony!\n\n" + result, "Sukces",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Brak pliku bazy danych do backupu.", "Info",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd backupu: " + ex.Message, "Błąd",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            // === PRZYCISK 7: Wyloguj ===
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

            sidebarPanel.Controls.AddRange(new Control[] 
            { 
                lockAllBtn, lockPatternBtn, checkpointBtn, 
                configBtn, historyBtn, backupBtn, logoutBtn 
            });

            // ========== PANEL GŁÓWNY ==========
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
                Size = new Size(490, 120),
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

            var welcomeInfo = new Label
            {
                Text = "Wybierz funkcję z menu po lewej stronie.",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 80),
                AutoSize = true,
                ForeColor = subtitleColor
            };

            welcomeCard.Controls.Add(welcomeTitle);
            welcomeCard.Controls.Add(welcomeSubtitle);
            welcomeCard.Controls.Add(welcomeInfo);

            // Karta statystyk
            var statsCard = new Panel
            {
                Location = new Point(20, 160),
                Size = new Size(490, 180),
                BackColor = Color.White
            };

            var statsTitle = new Label
            {
                Text = "📈  Statystyki systemu",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true,
                ForeColor = textColor
            };

            // Pobierz statystyki
            int totalPatterns = 0;
            int activePatterns = 0;
            int totalEvents = 0;

            try
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                if (File.Exists(dbPath))
                {
                    var json = File.ReadAllText(dbPath);
                    var data = JsonConvert.DeserializeObject<DatabaseData>(json);
                    if (data != null)
                    {
                        totalPatterns = data.Patterns?.Count ?? 0;
                        activePatterns = data.Patterns?.Count(p => p.IsActive) ?? 0;
                        totalEvents = data.EventLogs?.Count ?? 0;
                    }
                }
            }
            catch { }

            var statsText = new Label
            {
                Text = $"• Wszystkie wzorce (Pattern): {totalPatterns}\n" +
                       $"• Aktywne wzorce: {activePatterns}\n" +
                       $"• Nieaktywne wzorce: {totalPatterns - activePatterns}\n" +
                       $"• Zdarzenia w historii: {totalEvents}\n" +
                       $"• Baza danych: {(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json")) ? "✅ Połączona" : "❌ Brak")}",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 50),
                AutoSize = true,
                ForeColor = subtitleColor
            };

            statsCard.Controls.Add(statsTitle);
            statsCard.Controls.Add(statsText);

            // Przycisk odświeżania
            var refreshBtn = new Button
            {
                Text = "🔄  Odśwież statystyki",
                Location = new Point(20, 145),
                Size = new Size(160, 30),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            refreshBtn.FlatAppearance.BorderSize = 0;
            refreshBtn.Click += (s, e) => RefreshStats();
            statsCard.Controls.Add(refreshBtn);

            mainPanel.Controls.Add(welcomeCard);
            mainPanel.Controls.Add(statsCard);

            // Dodaj wszystko do formularza
            this.Controls.Add(headerPanel);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(shadowLine);
            this.Controls.Add(mainPanel);
        }

        /// <summary>
        /// Tworzy przycisk menu bocznego z efektem hover
        /// </summary>
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

            // Efekt hover
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

        /// <summary>
        /// Odświeża statystyki na dashboardzie
        /// </summary>
        private void RefreshStats()
        {
            try
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                if (File.Exists(dbPath))
                {
                    var json = File.ReadAllText(dbPath);
                    var data = JsonConvert.DeserializeObject<DatabaseData>(json);
                    
                    if (data != null)
                    {
                        int totalPatterns = data.Patterns?.Count ?? 0;
                        int activePatterns = data.Patterns?.Count(p => p.IsActive) ?? 0;
                        int totalEvents = data.EventLogs?.Count ?? 0;

                        MessageBox.Show(
                            $"📊 Statystyki odświeżone!\n\n" +
                            $"• Wszystkie patterny: {totalPatterns}\n" +
                            $"• Aktywne: {activePatterns}\n" +
                            $"• Zdarzenia: {totalEvents}",
                            "Odświeżanie", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd odświeżania: " + ex.Message, "Błąd");
            }
        }
    }
}