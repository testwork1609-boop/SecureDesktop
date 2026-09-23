using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SecureDesktop.Database
{
    public class DatabaseInitializer
    {
        private const int MaxEventLogs = 5000;
        private const int MaxSessions = 1000;

        private readonly string _dbPath;
        private readonly object _ioLock = new object();
        private DatabaseData _data;

        public DatabaseInitializer(string dbPath)
        {
            _dbPath = dbPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
        }

        public void Initialize()
        {
            lock (_ioLock)
            {
                try
                {
                    var dbDirectory = Path.GetDirectoryName(_dbPath);
                    if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
                        Directory.CreateDirectory(dbDirectory);

                    if (File.Exists(_dbPath))
                    {
                        var json = File.ReadAllText(_dbPath);
                        _data = JsonConvert.DeserializeObject<DatabaseData>(json);

                        if (_data == null)
                        {
                            string backupPath = _dbPath + ".backup_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                            File.Copy(_dbPath, backupPath, overwrite: true);
                            _data = new DatabaseData();
                        }
                    }
                    else
                    {
                        _data = new DatabaseData();
                        InsertDefaultData();
                        SaveInternal();
                        return;
                    }

                    EnsureCollections();
                    MigrateLegacyPasswordSetting();
                    MigrateDefaultShifts();
                }
                catch (Exception ex)
                {
                    throw new Exception("Database init failed: " + ex.Message, ex);
                }
            }
        }

        private void EnsureCollections()
        {
            if (_data == null) _data = new DatabaseData();
            if (_data.Users == null) _data.Users = new List<Models.User>();
            if (_data.Sessions == null) _data.Sessions = new List<Models.Session>();
            if (_data.EventLogs == null) _data.EventLogs = new List<Models.EventLog>();
            if (_data.Patterns == null) _data.Patterns = new List<Models.Pattern>();
            if (_data.Tips == null) _data.Tips = new List<Models.Tip>();
            if (_data.Shifts == null) _data.Shifts = new List<Models.Shift>();
            if (_data.Settings == null) _data.Settings = new Dictionary<string, string>();

            if (_data.NextTipId < 1)
                _data.NextTipId = (_data.Tips.Count > 0 ? _data.Tips.Max(t => t.Id) : 0) + 1;
            if (_data.NextPatternId < 1)
                _data.NextPatternId = (_data.Patterns.Count > 0 ? _data.Patterns.Max(p => p.Id) : 0) + 1;
            if (_data.NextUserId < 1)
                _data.NextUserId = (_data.Users.Count > 0 ? _data.Users.Max(u => u.Id) : 0) + 1;
            if (_data.NextShiftId < 1)
                _data.NextShiftId = (_data.Shifts.Count > 0 ? _data.Shifts.Max(s => s.Id) : 0) + 1;
        }

        /// <summary>
        /// Jeśli baza nie ma jeszcze zmian, tworzy 4 domyślne (A, B, C, A1)
        /// z hasłem "admin". Wszystkich użytkowników bez ShiftId przypisuje
        /// do pierwszej zmiany.
        /// </summary>
        private void MigrateDefaultShifts()
        {
            if (_data.Shifts == null) _data.Shifts = new List<Models.Shift>();

            if (_data.Shifts.Count == 0)
            {
                string[] names = { "Zmiana A", "Zmiana B", "Zmiana C", "Zmiana A1" };
                foreach (var name in names)
                {
                    var salt = Utils.SecurityHelper.GenerateSalt();
                    var hash = Utils.SecurityHelper.HashPassword("admin", salt);
                    _data.Shifts.Add(new Models.Shift
                    {
                        Id = _data.NextShiftId++,
                        Name = name,
                        Salt = salt,
                        PasswordHash = hash,
                        CreatedAt = DateTime.Now,
                        IsActive = true
                    });
                }
            }

            if (_data.Shifts.Count > 0 && _data.Users != null)
            {
                int defaultShiftId = _data.Shifts[0].Id;
                bool changed = false;
                foreach (var u in _data.Users)
                {
                    if (!u.ShiftId.HasValue)
                    {
                        u.ShiftId = defaultShiftId;
                        changed = true;
                    }
                }
                if (changed) SaveInternal();
            }
        }

        private void MigrateLegacyPasswordSetting()
        {
            if (_data == null || _data.Settings == null) return;
            if (!_data.Settings.TryGetValue("AdminPassword", out var legacy)) return;

            if (!string.IsNullOrEmpty(legacy) && _data.Users != null)
            {
                var admin = _data.Users.FirstOrDefault(u => u.IdentificationNumber == "admin");
                if (admin != null)
                {
                    admin.Salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                    admin.PasswordHash = Convert.ToBase64String(
                        System.Security.Cryptography.SHA256.Create().ComputeHash(
                            System.Text.Encoding.UTF8.GetBytes(legacy + admin.Salt)));
                }
            }

            // Nie usuwamy - zostaje jako fallback dla admina bez zmiany.
            SaveInternal();
        }

        private void InsertDefaultData()
        {
            var salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var hash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.Create().ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes("admin" + salt)));

            // Domyślne zmiany
            string[] names = { "Zmiana A", "Zmiana B", "Zmiana C", "Zmiana A1" };
            foreach (var name in names)
            {
                var sSalt = Utils.SecurityHelper.GenerateSalt();
                var sHash = Utils.SecurityHelper.HashPassword("admin", sSalt);
                _data.Shifts.Add(new Models.Shift
                {
                    Id = _data.NextShiftId++,
                    Name = name,
                    Salt = sSalt,
                    PasswordHash = sHash,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                });
            }

            _data.Users.Add(new Models.User
            {
                Id = 1,
                IdentificationNumber = "admin",
                PasswordHash = hash,
                Salt = salt,
                IsAdmin = true,
                CreatedAt = DateTime.Now,
                IsActive = true,
                ShiftId = _data.Shifts[0].Id
            });

            _data.Settings["PatternMatchThreshold"] = "0.95";
            _data.Settings["SearchInterval"] = "200";
            _data.Settings["AutoStart"] = "false";
            _data.Settings["MinimizeToTray"] = "true";
            _data.Settings["Theme"] = "Dark";
            _data.Settings["BackupPath"] = ".\\Backup";
            _data.Settings["AdminPassword"] = "admin";

            _data.Tips.Add(new Models.Tip
            {
                Id = 1,
                Title = "Witaj w SecureDesktop",
                Content = "Ta sekcja zawiera wskazówki dodane przez administratora. " +
                          "Możesz je edytować w Konfiguracja → Wskazówki.",
                CreatedAt = DateTime.Now,
                IsActive = true
            });
            _data.NextTipId = 2;
        }

        public void Save()
        {
            lock (_ioLock) SaveInternal();
        }

        private void SaveInternal()
        {
            TrimData();
            var json = JsonConvert.SerializeObject(_data, Formatting.Indented);

            var tmp = _dbPath + ".tmp";
            File.WriteAllText(tmp, json);

            if (File.Exists(_dbPath))
            {
                try
                {
                    File.Replace(tmp, _dbPath, _dbPath + ".bak", ignoreMetadataErrors: true);
                }
                catch
                {
                    File.Copy(tmp, _dbPath, overwrite: true);
                    try { File.Delete(tmp); } catch { }
                }
            }
            else
            {
                File.Move(tmp, _dbPath);
            }
        }

        private void TrimData()
        {
            if (_data == null) return;

            if (_data.EventLogs != null && _data.EventLogs.Count > MaxEventLogs)
                _data.EventLogs.RemoveRange(0, _data.EventLogs.Count - MaxEventLogs);

            if (_data.Sessions != null && _data.Sessions.Count > MaxSessions)
                _data.Sessions.RemoveRange(0, _data.Sessions.Count - MaxSessions);
        }

        public DatabaseData GetData() { return _data; }
        public string GetDatabasePath() { return _dbPath; }
    }

    public class DatabaseData
    {
        public List<Models.User> Users { get; set; } = new List<Models.User>();
        public List<Models.Session> Sessions { get; set; } = new List<Models.Session>();
        public List<Models.EventLog> EventLogs { get; set; } = new List<Models.EventLog>();
        public List<Models.Pattern> Patterns { get; set; } = new List<Models.Pattern>();
        public List<Models.Tip> Tips { get; set; } = new List<Models.Tip>();
        public List<Models.Shift> Shifts { get; set; } = new List<Models.Shift>();
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        public int NextUserId { get; set; } = 2;
        public int NextSessionId { get; set; } = 1;
        public int NextEventId { get; set; } = 1;
        public int NextPatternId { get; set; } = 1;
        public int NextTipId { get; set; } = 1;
        public int NextShiftId { get; set; } = 1;
    }
}
