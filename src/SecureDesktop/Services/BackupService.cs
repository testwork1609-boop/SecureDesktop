using System;
using System.IO;
using System.Linq;

namespace SecureDesktop.Services
{
    public class BackupService
    {
        // Czyszczenie starych backupów wymaga przeskanowania całego drzewa
        // katalogów backupu. Wcześniej robiono to synchronicznie przy KAŻDYM
        // pojedynczym CreateBackup() (np. przy każdym logowaniu użytkownika,
        // jeśli skonfigurowano monitorowany plik) - niepotrzebnie obciążając
        // dysk i wątek wywołujący. Teraz czyszczenie jest throttlowane do
        // maksymalnie raz na kilka godzin per proces.
        private static DateTime _lastCleanupUtc = DateTime.MinValue;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
        private static readonly object CleanupLock = new object();

        /// <summary>
        /// Tworzy kopię zapasową pliku w folderze z datą i godziną
        /// </summary>
        public string CreateBackup(string sourcePath, string backupRoot)
        {
            try
            {
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("Plik zrodlowy nie istnieje: " + sourcePath);

                // Format: Backup/2025-01-15/14-30-25/nazwa_pliku
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string timeFolder = DateTime.Now.ToString("HH-mm-ss");
                string backupDir = Path.Combine(backupRoot, dateFolder, timeFolder);

                Directory.CreateDirectory(backupDir);

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(backupDir, fileName);

                File.Copy(sourcePath, destPath, false);

                // Czyszczenie starych backupów (starsze niż 30 dni) - throttlowane,
                // nie wykonywane przy każdym pojedynczym backupie.
                TryCleanOldBackupsThrottled(backupRoot, 30);

                return destPath;
            }
            catch (Exception ex)
            {
                throw new Exception("Backup failed: " + ex.Message, ex);
            }
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

        /// <summary>
        /// Usuwa foldery backupów starsze niż określona liczba dni.
        /// Metoda publiczna pozostaje dostępna do jawnego wywołania
        /// (np. z przycisku "Wyczyść stare backupy" w UI), niezależnie
        /// od wewnętrznego throttlingu w CreateBackup.
        /// </summary>
        public void CleanOldBackups(string backupRoot, int maxDays)
        {
            try
            {
                if (!Directory.Exists(backupRoot))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-maxDays);

                foreach (var dateDir in Directory.GetDirectories(backupRoot))
                {
                    string folderName = Path.GetFileName(dateDir);

                    // Sprawdź czy nazwa folderu to data (format YYYY-MM-DD)
                    if (DateTime.TryParseExact(folderName, "yyyy-MM-dd", null,
                        System.Globalization.DateTimeStyles.None, out DateTime folderDate))
                    {
                        if (folderDate < cutoffDate)
                        {
                            try
                            {
                                Directory.Delete(dateDir, true);
                                System.Diagnostics.Debug.WriteLine($"Usunieto stary backup: {dateDir}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Nie mozna usunac {dateDir}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CleanOldBackups error: {ex.Message}");
            }
        }

        /// <summary>
        /// Zwraca informacje o pliku
        /// </summary>
        public FileInfo GetFileInfo(string filePath)
        {
            if (!File.Exists(filePath))
                return null;
            return new FileInfo(filePath);
        }

        /// <summary>
        /// Sprawdza czy plik się zmienił (porównanie hash SHA-256)
        /// </summary>
        public bool HasFileChanged(string filePath, string previousHash)
        {
            if (!File.Exists(filePath))
                return true;

            string currentHash = ComputeHash(filePath);
            return currentHash != previousHash;
        }

        /// <summary>
        /// Oblicza hash SHA-256 pliku
        /// </summary>
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
