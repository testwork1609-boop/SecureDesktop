using System;
using System.Drawing;
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
        private readonly PatternRecognitionService _patternService;

        public DashboardForm(User user, DatabaseInitializer db)
        {
            _currentUser = user;
            _db = db;
            _lockService = new ScreenLockService();
            _patternService = new PatternRecognitionService();
            
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "SecureDesktop - Panel Główny";
            this.Size = new Size(800, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Nagłówek
            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(800, 80),
                BackColor = Color.FromArgb(45, 45, 48)
            };

            var titleLabel = new Label
            {
                Text = "SecureDesktop",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };

            var userLabel = new Label
            {
                Text = $"Zalogowany: {_currentUser.IdentificationNumber}" + 
                       (_currentUser.IsAdmin ? " (Admin)" : ""),
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 50),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(userLabel);

            // Panel boczny z przyciskami
            var menuPanel = new Panel
            {
                Location = new Point(0, 80),
                Size = new Size(300, 570),
                BackColor = Color.FromArgb(40, 40, 45)
            };

            int yPos = 20;

            var lockAllBtn = CreateMenuButton("🔒 Blokuj cały ekran", yPos);
            lockAllBtn.Click += (s, e) =>
            {
                try
                {
                    _lockService.LockAllScreens();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd blokowania: " + ex.Message, 
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            var lockPatternBtn = CreateMenuButton("🎯 Blokuj z Pattern", yPos);
            lockPatternBtn.Click += (s, e) =>
            {
                MessageBox.Show(
                    "Funkcja blokowania z wzorcami\n\n" +
                    "Aby użyć:\n" +
                    "1. Przejdź do Konfiguracji\n" +
                    "2. Dodaj wzorce ekranowe\n" +
                    "3. Włącz blokadę Pattern\n\n" +
                    "Ta funkcja będzie dostępna w pełnej wersji.",
                    "Pattern Lock - Informacja",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };
            yPos += 55;

            var checkpointBtn = CreateMenuButton("⚡ CheckPoint", yPos);
            checkpointBtn.Click += (s, e) =>
            {
                try
                {
                    // Domyślnie uruchamia Notatnik
                    // W przyszłości będzie czytać z konfiguracji
                    System.Diagnostics.Process.Start("notepad.exe");
                    
                    // Logowanie zdarzenia
                    var eventRepo = new Database.Repositories.EventLogRepository(_db);
                    eventRepo.Create(new EventLog
                    {
                        UserId = _currentUser.Id,
                        IdentificationNumber = _currentUser.IdentificationNumber,
                        OperationName = "CheckPoint",
                        Result = "Success",
                        Description = "Uruchomiono notepad.exe"
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd CheckPoint: " + ex.Message,
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            var configBtn = CreateMenuButton("⚙️ Konfiguracja", yPos);
            configBtn.Click += (s, e) =>
            {
                var configForm = new ConfigurationForm();
                configForm.ShowDialog(this);
            };
            yPos += 55;

            var historyBtn = CreateMenuButton("📊 Historia zdarzeń", yPos);
            historyBtn.Click += (s, e) =>
            {
                var historyForm = new EventHistoryForm();
                historyForm.ShowDialog(this);
            };
            yPos += 55;

            var backupBtn = CreateMenuButton("💾 Wykonaj backup", yPos);
            backupBtn.Click += (s, e) =>
            {
                try
                {
                    var backupService = new BackupService();
                    var sourcePath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                    
                    if (System.IO.File.Exists(sourcePath))
                    {
                        var backupPath = backupService.CreateBackup(sourcePath, "Backup");
                        MessageBox.Show("Backup utworzony!\n" + backupPath,
                            "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Brak pliku bazy danych do backupu.",
                            "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd backupu: " + ex.Message,
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            yPos += 55;

            var logoutBtn = CreateMenuButton("🚪 Wyloguj", yPos);
            logoutBtn.BackColor = Color.FromArgb(180, 50, 50);
            logoutBtn.Click += (s, e) =>
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
                
                this.Close();
            };

            menuPanel.Controls.AddRange(new Control[]
            {
                lockAllBtn, lockPatternBtn, checkpointBtn,
                configBtn, historyBtn, backupBtn, logoutBtn
            });

            // Panel główny (prawa strona)
            var mainPanel = new Panel
            {
                Location = new Point(300, 80),
                Size = new Size(500, 570),
                BackColor = Color.FromArgb(32, 32, 32)
            };

            var infoTitle = new Label
            {
                Text = "Informacje o systemie",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };

            var infoText = new Label
            {
                Text = $"Czas zalogowania: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Użytkownik: {_currentUser.IdentificationNumber}\n" +
                       $"Uprawnienia: {(_currentUser.IsAdmin ? "Administrator" : "Użytkownik")}\n\n" +
                       $"Dostępne funkcje:\n" +
                       $"• Blokada pełnoekranowa\n" +
                       $"• Blokada z wzorcami\n" +
                       $"• Uruchamianie aplikacji\n" +
                       $"• Backup bazy danych\n" +
                       $"• Historia zdarzeń\n" +
                       $"• Konfiguracja systemu\n\n" +
                       $"Wersja: 1.0.0\n" +
                       $"Środowisko: .NET Framework 4.8",
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 60),
                AutoSize = true,
                ForeColor = Color.LightGray
            };

            mainPanel.Controls.Add(infoTitle);
            mainPanel.Controls.Add(infoText);

            // Dodaj wszystkie panele do formularza
            this.Controls.Add(headerPanel);
            this.Controls.Add(menuPanel);
            this.Controls.Add(mainPanel);
        }

        private Button CreateMenuButton(string text, int yPosition)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(20, yPosition),
                Size = new Size(260, 45),
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0)
            };

            // Efekt hover
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(0, 140, 240);
            btn.MouseLeave += (s, e) =>
            {
                if (btn.Text.Contains("Wyloguj"))
                    btn.BackColor = Color.FromArgb(180, 50, 50);
                else
                    btn.BackColor = Color.FromArgb(0, 120, 212);
            };

            return btn;
        }
    }
}