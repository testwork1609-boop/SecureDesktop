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

                    MigrateLegacyPasswordSetting();
                }
                catch (Exception ex)
                {
                    throw new Exception("Database init failed: " + ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// Jednorazowa migracja: jeśli w Settings istnieje stare pole
        /// "AdminPassword" (plaintext), przenosimy je do User.PasswordHash
        /// (z nową solą) i usuwamy z Settings. Dzięki temu hasło przestaje
        /// leżeć jawnym tekstem w database.json.
        /// </summary>
        private void MigrateLegacyPasswordSetting()
        {
            if (_data?.Settings == null) return;
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

            _data.Settings.Remove("AdminPassword");
            SaveInternal();
        }

        private void InsertDefaultData()
        {
            var salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var hash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.Create().ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes("admin" + salt)
                )
            );

            _data.Users.Add(new Models.User
            {
                Id = 1,
                IdentificationNumber = "admin",
                PasswordHash = hash,
                Salt = salt,
                IsAdmin = true,
                CreatedAt = DateTime.Now,
                IsActive = true
            });

            _data.Settings["PatternMatchThreshold"] = "0.95";
            _data.Settings["SearchInterval"] = "200";
            _data.Settings["AutoStart"] = "false";
            _data.Settings["MinimizeToTray"] = "true";
            _data.Settings["Theme"] = "Dark";
            _data.Settings["BackupPath"] = ".\\Backup";
        }

        public void Save()
        {
            lock (_ioLock) SaveInternal();
        }

        private void SaveInternal()
        {
            TrimData();
            var json = JsonConvert.SerializeObject(_data, Formatting.Indented);

            // Atomowy zapis: najpierw tmp, potem File.Replace. Zapobiega
            // uszkodzeniu database.json, gdyby aplikacja padła w połowie zapisu.
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

        public DatabaseData GetData() => _data;
        public string GetDatabasePath() => _dbPath;
    }

    public class DatabaseData
    {
        public List<Models.User> Users { get; set; } = new List<Models.User>();
        public List<Models.Session> Sessions { get; set; } = new List<Models.Session>();
        public List<Models.EventLog> EventLogs { get; set; } = new List<Models.EventLog>();
        public List<Models.Pattern> Patterns { get; set; } = new List<Models.Pattern>();
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        public int NextUserId { get; set; } = 2;
        public int NextSessionId { get; set; } = 1;
        public int NextEventId { get; set; } = 1;
        public int NextPatternId { get; set; } = 1;
    }
}
