using System;
using System.IO;

namespace SecureDesktop.Services
{
    public class BackupService
    {
        private static DateTime _lastCleanupUtc = DateTime.MinValue;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
        private static readonly object CleanupLock = new object();

        /// <summary>
        /// Klasyczny backup. Format: Backup/yyyy-MM-dd/HH-mm-ss/nazwa_pliku
        /// </summary>
        public string CreateBackup(string sourcePath, string backupRoot)
        {
            try
            {
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("Plik zrodlowy nie istnieje: " + sourcePath);

                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string timeFolder = DateTime.Now.ToString("HH-mm-ss");
                string backupDir = Path.Combine(backupRoot, dateFolder, timeFolder);

                Directory.CreateDirectory(backupDir);

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(backupDir, fileName);

                File.Copy(sourcePath, destPath, false);
                return destPath;
            }
            catch (Exception ex)
            {
                throw new Exception("Backup failed: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Backup użytkownika — folder w formacie:
        ///     {backupRoot}/{yyyy-MM-dd_HH-mm-ss}_{PIN}/{nazwa_pliku}
        /// </summary>
        public string CreateUserBackup(string sourcePath, string backupRoot, string userPin)
        {
            try
            {
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("Plik zrodlowy nie istnieje: " + sourcePath);

                string safePin = SanitizeFileName(string.IsNullOrWhiteSpace(userPin) ? "unknown" : userPin);
                string folderName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + safePin;
                string backupDir = Path.Combine(backupRoot, folderName);

                Directory.CreateDirectory(backupDir);

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(backupDir, fileName);

                File.Copy(sourcePath, destPath, false);

                // Przy okazji pojedynczego backupu - throttlowane czyszczenie.
                TryCleanOldBackupsThrottled(backupRoot, GetConfiguredRetentionOrDefault());

                return destPath;
            }
            catch (Exception ex)
            {
                throw new Exception("Backup failed: " + ex.Message, ex);
            }
        }

        private static int GetConfiguredRetentionOrDefault()
        {
            // Domyślne 30 dni. Właściwa wartość jest brana z Settings i przekazywana
            // z Program.cs przez PurgeOldBackups(); tutaj tylko bezpieczna wartość.
            return 30;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        /// <summary>
        /// Trwale usuwa foldery backupu starsze niż retentionDays.
        ///
        /// Directory.Delete(path, true) w .NET Framework kasuje pliki FIZYCZNIE,
        /// bez wysyłania ich do kosza systemowego (inaczej niż przez Eksplorator).
        ///
        /// Obsługuje oba formaty folderów:
        ///     yyyy-MM-dd                (stary)
        ///     yyyy-MM-dd_HH-mm-ss_PIN   (nowy)
        ///
        /// Zwraca liczbę usuniętych folderów.
        /// </summary>
        public int PurgeOldBackups(string backupRoot, int retentionDays)
        {
            int deleted = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(backupRoot)) return 0;
                if (retentionDays <= 0) return 0; // 0 = wyłączone
                if (!Directory.Exists(backupRoot)) return 0;

                var cutoffDate = DateTime.Now.Date.AddDays(-retentionDays);

                foreach (var dir in Directory.GetDirectories(backupRoot))
                {
                    try
                    {
                        string folderName = Path.GetFileName(dir);
                        if (string.IsNullOrEmpty(folderName) || folderName.Length < 10) continue;

                        string datePart = folderName.Substring(0, 10);
                        DateTime folderDate;
                        if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd", null,
                            System.Globalization.DateTimeStyles.None, out folderDate))
                            continue;

                        if (folderDate < cutoffDate)
                        {
                            Directory.Delete(dir, true);
                            deleted++;
                            System.Diagnostics.Debug.WriteLine("PurgeOldBackups: usunieto " + dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "PurgeOldBackups: nie mozna usunac " + dir + " - " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PurgeOldBackups error: " + ex.Message);
            }

            return deleted;
        }

        private void TryCleanOldBackupsThrottled(string backupRoot, int maxDays)
        {
            lock (CleanupLock)
            {
                if (DateTime.UtcNow - _lastCleanupUtc < CleanupInterval)
                    return;
                _lastCleanupUtc = DateTime.UtcNow;
            }
            PurgeOldBackups(backupRoot, maxDays);
        }

        /// <summary>
        /// Backward-compat — stara nazwa, deleguje do PurgeOldBackups.
        /// </summary>
        public void CleanOldBackups(string backupRoot, int maxDays)
        {
            PurgeOldBackups(backupRoot, maxDays);
        }

        public FileInfo GetFileInfo(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            return new FileInfo(filePath);
        }

        public bool HasFileChanged(string filePath, string previousHash)
        {
            if (!File.Exists(filePath)) return true;
            string currentHash = ComputeHash(filePath);
            return currentHash != previousHash;
        }

        public string ComputeHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
