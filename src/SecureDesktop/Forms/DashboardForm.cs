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

        public DashboardForm(User user, DatabaseInitializer db)
        {
            _currentUser = user;
            _db = db;
            _lockService = new ScreenLockService();
            
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "SecureDesktop - Panel Glowny";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            var titleLabel = new Label
            {
                Text = $"Witaj, {_currentUser.IdentificationNumber}",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };

            var lockAllBtn = CreateButton("Blokuj caly ekran", new Point(50, 100));
            lockAllBtn.Click += (s, e) => _lockService.LockAllScreens();

            var lockPatternBtn = CreateButton("Blokuj z Pattern", new Point(50, 160));
            var checkpointBtn = CreateButton("CheckPoint", new Point(50, 220));
            var configBtn = CreateButton("Konfiguracja", new Point(50, 280));
            configBtn.Click += (s, e) => new ConfigurationForm().ShowDialog(this);
            
            var historyBtn = CreateButton("Historia zdarzen", new Point(50, 340));
            historyBtn.Click += (s, e) => new EventHistoryForm().ShowDialog(this);

            var logoutBtn = CreateButton("Wyloguj", new Point(50, 400));
            logoutBtn.BackColor = Color.FromArgb(180, 50, 50);
            logoutBtn.Click += (s, e) => this.Close();

            Controls.AddRange(new Control[] { titleLabel, lockAllBtn, lockPatternBtn, 
                                             checkpointBtn, configBtn, historyBtn, logoutBtn });
        }

        private Button CreateButton(string text, Point location)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = new Size(250, 45),
                Font = new Font("Segoe UI", 11),
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
        }
    }
}