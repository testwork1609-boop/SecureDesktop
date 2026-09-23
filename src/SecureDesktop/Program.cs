using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Utils;

namespace SecureDesktop
{
    static class Program
    {
        public static Icon AppIcon;
        private static DatabaseInitializer _globalDb;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Loc.LoadFromDisk();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppIcon = CreateLockIcon();

            try
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                _globalDb = new DatabaseInitializer(dbPath);
                _globalDb.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Nie można zainicjalizować bazy danych:\n\n" + ex.Message,
                    "SecureDesktop - błąd krytyczny",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            foreach (var dir in new[] { "Database", "Backup", "Logs" })
            {
                try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            }

            // === Automatyczne czyszczenie starych backupów przy starcie ===
            PurgeOldBackupsOnStartup();

            while (true)
            {
                using (var loginForm = new Forms.LoginForm(_globalDb))
                {
                    loginForm.Icon = AppIcon;
                    var result = loginForm.ShowDialog();

                    if (result == DialogResult.OK && loginForm.LoggedInUser != null)
                    {
                        PerformBackupAfterLogin(_globalDb);

                        using (var dashboard = new Forms.DashboardForm(
                            loginForm.LoggedInUser, _globalDb, loginForm.LoggedInSessionId))
                        {
                            dashboard.Icon = AppIcon;
                            Application.Run(dashboard);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Przy każdym uruchomieniu: odczytaj BackupPath i BackupRetentionDays
        /// z Settings, następnie usuń TRWALE foldery starsze niż retention.
        /// </summary>
                private static void PurgeOldBackupsOnStartup()
        {
            try
            {
                var data = _globalDb.GetData();
                if (data == null || data.Settings == null) return;

                string backupPath = "Backup";
                string bp;
                if (data.Settings.TryGetValue("BackupPath", out bp) && !string.IsNullOrWhiteSpace(bp))
                    backupPath = bp;

                int retentionDays = 30;
                string rd;
                if (data.Settings.TryGetValue("BackupRetentionDays", out rd))
                {
                    int parsed;
                    if (int.TryParse(rd, out parsed) && parsed > 0)
                        retentionDays = parsed;
                    else if (int.TryParse(rd, out parsed) && parsed == 0)
                        return; // 0 = wyłączone
                }

                // Ścieżka relatywna -> absolutna (względem katalogu exe).
                if (!Path.IsPathRooted(backupPath))
                    backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, backupPath);

                if (!Directory.Exists(backupPath)) return;

                var svc = new Services.BackupService();
                int deleted = svc.PurgeOldBackups(backupPath, retentionDays);

                System.Diagnostics.Debug.WriteLine(
                    "PurgeOldBackupsOnStartup: usunieto " + deleted +
                    " folderow starszych niz " + retentionDays + " dni (" + backupPath + ")");

                // === Log zdarzenia do dziennika ===
                if (deleted > 0)
                {
                    try
                    {
                        var eventRepo = new Database.Repositories.EventLogRepository(_globalDb);
                        eventRepo.Create(new Models.EventLog
                        {
                            UserId = null,
                            IdentificationNumber = "SYSTEM",
                            OperationName = "BackupPurge",
                            Result = "Success",
                            Severity = "Info",
                            Description = Loc.T("log.backup_purge", deleted, retentionDays)
                        });
                    }
                    catch (Exception exLog)
                    {
                        System.Diagnostics.Debug.WriteLine("PurgeOldBackupsOnStartup log error: " + exLog.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PurgeOldBackupsOnStartup error: " + ex.Message);
            }
        }

        private static void PerformBackupAfterLogin(DatabaseInitializer db)
        {
            try
            {
                var data = db.GetData();
                if (data == null || data.Settings == null) return;

                if (!data.Settings.TryGetValue("MonitoredFile", out var monitoredFile) ||
                    string.IsNullOrWhiteSpace(monitoredFile))
                    return;

                if (!File.Exists(monitoredFile)) return;

                string backupFolder = data.Settings.TryGetValue("BackupPath", out var bp) && !string.IsNullOrWhiteSpace(bp)
                    ? bp
                    : "Backup";

                var backupService = new Services.BackupService();

                if (data.Settings.TryGetValue("MonitoredFileBaselineHash", out var baselineHash) &&
                    !backupService.HasFileChanged(monitoredFile, baselineHash))
                    return;

                string backupPath = backupService.CreateBackup(monitoredFile, backupFolder);

                data.Settings["MonitoredFileBaselineHash"] = backupService.ComputeHash(monitoredFile);
                data.Settings["MonitoredFileLastNotifiedHash"] = data.Settings["MonitoredFileBaselineHash"];
                db.Save();

                var eventRepo = new Database.Repositories.EventLogRepository(db);
                eventRepo.Create(new Models.EventLog
                {
                    OperationName = "Backup",
                    Result = "Success",
                    Description = "Backup pliku: " + monitoredFile + " -> " + backupPath
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Backup error: " + ex.Message);
            }
        }

        private static Icon CreateLockIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(45, 165, 90));
                    using (var brush = new SolidBrush(Color.White))
                        g.FillRectangle(brush, 7, 14, 18, 14);
                    using (var pen = new Pen(Color.White, 3))
                        g.DrawArc(pen, 9, 4, 14, 14, 180, 180);
                    using (var brush = new SolidBrush(Color.FromArgb(45, 165, 90)))
                    {
                        g.FillEllipse(brush, 13, 18, 6, 5);
                        g.FillRectangle(brush, 15, 21, 2, 5);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (var tempIcon = Icon.FromHandle(hIcon))
                        return (Icon)tempIcon.Clone();
                }
                finally { DestroyIcon(hIcon); }
            }
        }
    }
}
