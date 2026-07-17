using System;
using System.IO;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;
using SecureDesktop.Utils;

namespace SecureDesktop.Services
{
    /// <summary>
    /// Sprawdza, czy plik wskazany jako "monitorowany" zmienil sie od czasu ostatniego
    /// wykonanego backupu, i jesli tak - zapisuje odpowiedni wpis w historii zdarzen.
    ///
    /// Sprawdzenie NIE dziala w tle na timerze - jest wywolywane recznie (CheckNow) w
    /// momentach blokady/odblokowania ekranu, patrz DashboardForm.
    /// </summary>
    public class FileMonitorService
    {
        private readonly DatabaseInitializer _db;
        private readonly EventLogRepository _eventRepo;

        private const string SettingMonitoredFile = "MonitoredFile";
        private const string SettingBaselineHash = "MonitoredFileBaselineHash";
        private const string SettingLastNotifiedHash = "MonitoredFileLastNotifiedHash";

        public FileMonitorService(DatabaseInitializer db)
        {
            _db = db;
            _eventRepo = new EventLogRepository(db);
        }

        /// <summary>
        /// Zapisuje aktualny hash pliku jako punkt odniesienia ("stan zgodny z backupem").
        /// Wywolywac zaraz po kazdym udanym backupie monitorowanego pliku (przy logowaniu
        /// i przy recznym backupie z zakladki "Backup").
        /// </summary>
        public void SaveBaseline(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

                string hash = SecurityHelper.ComputeFileHash(filePath);
                var settings = _db.GetData().Settings;
                settings[SettingBaselineHash] = hash;
                settings[SettingLastNotifiedHash] = hash;
                _db.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("FileMonitorService.SaveBaseline error: " + ex.Message);
            }
        }

        /// <summary>
        /// Usuwa zapisany punkt odniesienia - wywolywac gdy uzytkownik zmieni sciezke
        /// monitorowanego pliku w Konfiguracji, zeby nie porownywac nowego pliku z
        /// hashem poprzedniego.
        /// </summary>
        public void ResetBaseline()
        {
            var settings = _db.GetData()?.Settings;
            if (settings == null) return;

            settings.Remove(SettingBaselineHash);
            settings.Remove(SettingLastNotifiedHash);
            _db.Save();
        }

        /// <summary>
        /// Sprawdza teraz, czy monitorowany plik rozni sie od ostatniego zapisanego
        /// punktu odniesienia (ostatniego backupu). Wywolywane przy blokadzie i
        /// odblokowaniu ekranu.
        /// </summary>
        public void CheckNow(string context = null)
        {
            try
            {
                var data = _db.GetData();
                var settings = data?.Settings;
                if (settings == null) return;

                if (!settings.ContainsKey(SettingMonitoredFile) || string.IsNullOrWhiteSpace(settings[SettingMonitoredFile]))
                    return;

                string filePath = settings[SettingMonitoredFile];
                if (!File.Exists(filePath))
                    return;

                string currentHash = SecurityHelper.ComputeFileHash(filePath);
                string baselineHash = settings.ContainsKey(SettingBaselineHash) ? settings[SettingBaselineHash] : null;

                // Brak zapisanego punktu odniesienia (np. plik wskazany, ale jeszcze
                // nie wykonano backupu) - ustaw biezacy stan jako punkt odniesienia,
                // zeby nie zglaszac falszywej zmiany od razu.
                if (baselineHash == null)
                {
                    settings[SettingBaselineHash] = currentHash;
                    settings[SettingLastNotifiedHash] = currentHash;
                    _db.Save();
                    return;
                }

                if (currentHash == baselineHash)
                    return; // plik zgodny z wersja z ostatniego backupu

                string lastNotifiedHash = settings.ContainsKey(SettingLastNotifiedHash) ? settings[SettingLastNotifiedHash] : null;

                if (currentHash == lastNotifiedHash)
                    return; // ta konkretna zmiana zostala juz zgloszona - nie duplikuj wpisu

                settings[SettingLastNotifiedHash] = currentHash;
                _db.Save();

                string when = string.IsNullOrEmpty(context) ? "" : $" ({context})";

                _eventRepo.Create(new EventLog
                {
                    OperationName = "FileModified",
                    Result = "Warning",
                    Severity = "Warning",
                    Description = $"Wykryto zmiane monitorowanego pliku{when}: {filePath}. " +
                                  "Plik rozni sie od wersji z ostatniego backupu - sprawdz, ktory backup nalezy wczytac."
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("FileMonitorService.CheckNow error: " + ex.Message);
            }
        }
    }
}