using System;
using System.IO;
using System.Linq;

namespace SecureDesktop.Services
{
    public class BackupService
    {
        private static DateTime _lastCleanupUtc = DateTime.MinValue;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
        private static readonly object CleanupLock = new object();

        /// <summary>
        /// Klasyczny backup (używany przez Program.cs przy logowaniu, dla pliku monitorowanego).
        /// Format: Backup/yyyy-MM-dd/HH-mm-ss/nazwa_pliku
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

                TryCleanOldBackupsThrottled(backupRoot, 30);

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
        ///
        /// Przykład: Backup\2025-09-23_14-32-15_1234\faktura.xlsx
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

                TryCleanOldBackupsThrottled(backupRoot, 30);

                return destPath;
            }
            catch (Exception ex)
            {
                throw new Exception("Backup failed: " + ex.Message, ex);
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void TryCleanOldBackupsThrottled(string backupRoot, int maxDays)
        {
            lock (CleanupLock)
            {
                if (DateTime.UtcNow - _lastCleanupUtc < CleanupInterval)
                    return;
                _lastCleanupUtc = DateTime.UtcNow;
            }
            CleanOldBackups(backupRoot, maxDays);
        }

        public void CleanOldBackups(string backupRoot, int maxDays)
        {
            try
            {
                if (!Directory.Exists(backupRoot)) return;

                var cutoffDate = DateTime.Now.AddDays(-maxDays);

                // Stary format: yyyy-MM-dd
                foreach (var dateDir in Directory.GetDirectories(backupRoot))
                {
                    string folderName = Path.GetFileName(dateDir);
                    DateTime folderDate;

                    // Nowy format: yyyy-MM-dd_HH-mm-ss_PIN — bierzemy pierwsze 10 znaków
                    if (folderName.Length >= 10)
                    {
                        string datePart = folderName.Substring(0, 10);
                        if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", null,
                            System.Globalization.DateTimeStyles.None, out folderDate))
                        {
                            if (folderDate < cutoffDate)
                            {
                                try { Directory.Delete(dateDir, true); }
                                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Delete " + dateDir + ": " + ex.Message); }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CleanOldBackups error: " + ex.Message);
            }
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
