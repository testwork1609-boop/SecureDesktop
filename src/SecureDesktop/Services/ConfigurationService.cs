using System.Collections.Generic;
using System.Data.SQLite;
using Dapper;

namespace SecureDesktop.Services
{
    public class ConfigurationService
    {
        private readonly string _connectionString;

        public ConfigurationService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public string GetSetting(string key)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                return conn.QuerySingleOrDefault<string>(
                    "SELECT SettingValue FROM Settings WHERE SettingKey = @Key",
                    new { Key = key });
            }
        }

        public void SetSetting(string key, string value)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Execute(@"
                    INSERT OR REPLACE INTO Settings (SettingKey, SettingValue, UpdatedAt)
                    VALUES (@Key, @Value, CURRENT_TIMESTAMP)",
                    new { Key = key, Value = value });
            }
        }
    }
}