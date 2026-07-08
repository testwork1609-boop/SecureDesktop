using System;
using System.Data.SQLite;
using System.IO;
using Serilog;

namespace SecureDesktop.Database
{
    public class DatabaseInitializer
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public DatabaseInitializer(string dbPath = null)
        {
            _dbPath = dbPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "SecureDesktop.db");
            _connectionString = $"Data Source={_dbPath};Version=3;";
        }

        public void Initialize()
        {
            try
            {
                var dbDirectory = Path.GetDirectoryName(_dbPath);
                if (!Directory.Exists(dbDirectory))
                    Directory.CreateDirectory(dbDirectory);

                if (!File.Exists(_dbPath))
                {
                    SQLiteConnection.CreateFile(_dbPath);
                    Log.Information("Database created at: {Path}", _dbPath);
                }

                CreateTables();
                InsertDefaultData();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database initialization failed");
                throw;
            }
        }

        private void CreateTables()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                string[] sqlCommands = {
                    @"CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IdentificationNumber TEXT UNIQUE NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        Salt TEXT NOT NULL,
                        IsAdmin INTEGER DEFAULT 0,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        LastLoginAt DATETIME,
                        IsActive INTEGER DEFAULT 1
                    )",
                    
                    @"CREATE TABLE IF NOT EXISTS Settings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        SettingKey TEXT UNIQUE NOT NULL,
                        SettingValue TEXT NOT NULL,
                        Description TEXT,
                        UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    )",
                    
                    @"CREATE TABLE IF NOT EXISTS Patterns (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        ImageData BLOB NOT NULL,
                        MarginTop INTEGER DEFAULT 10,
                        MarginBottom INTEGER DEFAULT 10,
                        MarginLeft INTEGER DEFAULT 10,
                        MarginRight INTEGER DEFAULT 10,
                        IsActive INTEGER DEFAULT 1,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME
                    )",
                    
                    @"CREATE TABLE IF NOT EXISTS Sessions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        IdentificationNumber TEXT NOT NULL,
                        LoginTime DATETIME DEFAULT CURRENT_TIMESTAMP,
                        LogoutTime DATETIME,
                        SessionToken TEXT NOT NULL,
                        IsActive INTEGER DEFAULT 1,
                        FOREIGN KEY (UserId) REFERENCES Users(Id)
                    )",
                    
                    @"CREATE TABLE IF NOT EXISTS EventLog (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER,
                        IdentificationNumber TEXT,
                        OperationName TEXT NOT NULL,
                        Result TEXT NOT NULL,
                        Description TEXT,
                        Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                        Severity TEXT DEFAULT 'Info',
                        FOREIGN KEY (UserId) REFERENCES Users(Id)
                    )",
                    
                    @"CREATE TABLE IF NOT EXISTS FileMonitor (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FilePath TEXT NOT NULL,
                        LastCheckTime DATETIME,
                        LastFileSize INTEGER,
                        LastModifiedDate DATETIME,
                        FileHash TEXT,
                        UserId INTEGER,
                        FOREIGN KEY (UserId) REFERENCES Users(Id)
                    )",
                    
                    @"CREATE TABLE IF NOT EXISTS BackupHistory (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OriginalFilePath TEXT NOT NULL,
                        BackupFilePath TEXT NOT NULL,
                        FileSize INTEGER,
                        FileHash TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UserId INTEGER,
                        FOREIGN KEY (UserId) REFERENCES Users(Id)
                    )"
                };

                foreach (var sql in sqlCommands)
                {
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void InsertDefaultData()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Open();

                var checkSql = "SELECT COUNT(*) FROM Users WHERE IdentificationNumber = 'admin'";
                using (var cmd = new SQLiteCommand(checkSql, conn))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    {
                        var salt = Utils.SecurityHelper.GenerateSalt();
                        var hash = Utils.SecurityHelper.HashPassword("admin", salt);
                        
                        var insertSql = @"INSERT INTO Users (IdentificationNumber, PasswordHash, Salt, IsAdmin) 
                                        VALUES ('admin', @Hash, @Salt, 1)";
                        using (var insertCmd = new SQLiteCommand(insertSql, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@Hash", hash);
                            insertCmd.Parameters.AddWithValue("@Salt", salt);
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }

                var settings = new[] {
                    "INSERT OR IGNORE INTO Settings (SettingKey, SettingValue, Description) VALUES ('PatternMatchThreshold', '0.95', 'Template matching threshold')",
                    "INSERT OR IGNORE INTO Settings (SettingKey, SettingValue, Description) VALUES ('SearchInterval', '500', 'Search interval in ms')",
                    "INSERT OR IGNORE INTO Settings (SettingKey, SettingValue, Description) VALUES ('AutoStart', 'false', 'Auto-start with Windows')",
                    "INSERT OR IGNORE INTO Settings (SettingKey, SettingValue, Description) VALUES ('MinimizeToTray', 'true', 'Minimize to tray')",
                    "INSERT OR IGNORE INTO Settings (SettingKey, SettingValue, Description) VALUES ('Theme', 'Dark', 'Application theme')"
                };

                foreach (var sql in settings)
                {
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public string GetConnectionString() => _connectionString;
    }
}