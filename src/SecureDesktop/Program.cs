using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SecureDesktop.Database;

namespace SecureDesktop
{
    static class Program
    {
        public static Icon AppIcon;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppIcon = CreateLockIcon();

            while (true)
            {
                foreach (var dir in new[] { "Database", "Backup", "Logs" })
                {
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }

                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                var db = new Database.DatabaseInitializer(dbPath);
                db.Initialize();

                using (var loginForm = new Forms.LoginForm(db))
                {
                    loginForm.Icon = AppIcon;
                    var result = loginForm.ShowDialog();

                    if (result == DialogResult.OK)
                    {
                        PerformBackupAfterLogin(db);

                        var dashboard = new Forms.DashboardForm(loginForm.LoggedInUser, db);
                        dashboard.Icon = AppIcon;
                        Application.Run(dashboard);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private static void PerformBackupAfterLogin(DatabaseInitializer db)
        {
            try
            {
                var data = db.GetData();
                if (data == null || data.Settings == null) return;

                if (!data.Settings.ContainsKey("MonitoredFile") ||
                    string.IsNullOrWhiteSpace(data.Settings["MonitoredFile"]))
                    return;

                string monitoredFile = data.Settings["MonitoredFile"];
                if (!File.Exists(monitoredFile)) return;

                string backupFolder = data.Settings.ContainsKey("BackupPath")
                    ? data.Settings["BackupPath"]
                    : "Backup";

                var backupService = new Services.BackupService();
                string backupPath = backupService.CreateBackup(monitoredFile, backupFolder);

                var eventRepo = new Database.Repositories.EventLogRepository(db);
                eventRepo.Create(new Models.EventLog
                {
                    OperationName = "Backup",
                    Result = "Success",
                    Description = $"Backup pliku: {monitoredFile} -> {backupPath}"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backup error: {ex.Message}");
            }
        }

        private static Icon CreateLockIcon()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(45, 165, 90));
                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(brush, 7, 14, 18, 14);
                }
                using (var pen = new Pen(Color.White, 3))
                {
                    g.DrawArc(pen, 9, 4, 14, 14, 180, 180);
                }
                using (var brush = new SolidBrush(Color.FromArgb(45, 165, 90)))
                {
                    g.FillEllipse(brush, 13, 18, 6, 5);
                    g.FillRectangle(brush, 15, 21, 2, 5);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }
}