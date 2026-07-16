using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SecureDesktop.Database
{
    public class DatabaseInitializer
    {
        private readonly string _dbPath;
        private DatabaseData _data;

        public DatabaseInitializer(string dbPath)
        {
            _dbPath = dbPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
        }

        public void Initialize()
        {
            try
            {
                var dbDirectory = Path.GetDirectoryName(_dbPath);
                if (!Directory.Exists(dbDirectory))
                    Directory.CreateDirectory(dbDirectory);

                if (File.Exists(_dbPath))
                {
                    var json = File.ReadAllText(_dbPath);
                    _data = JsonConvert.DeserializeObject<DatabaseData>(json) ?? new DatabaseData();
                }
                else
                {
                    _data = new DatabaseData();
                    InsertDefaultData();
                    Save();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Database init failed: " + ex.Message, ex);
            }
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
            _data.Settings["AdminPassword"] = "admin";
            _data.Settings["BackupPath"] = ".\\Backup";
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(_data, Formatting.Indented);
            File.WriteAllText(_dbPath, json);
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
    }
}