using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SecureDesktop.Database
{
    public class DatabaseInitializer
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public DatabaseInitializer(string dbPath)
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath}";
        }

        public void Initialize()
        {
            try
            {
                var dbDirectory = Path.GetDirectoryName(_dbPath);
                if (!Directory.Exists(dbDirectory))
                    Directory.CreateDirectory(dbDirectory);

                CreateTables();
                InsertDefaultData();
            }
            catch (Exception ex)
            {
                throw new Exception("Database init failed: " + ex.Message, ex);
            }
        }

        private void CreateTables()
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();

                string sql = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IdentificationNumber TEXT UNIQUE NOT NULL,
                        PasswordHash TEXT NOT NULL,
                        Salt TEXT NOT NULL,
                        IsAdmin INTEGER DEFAULT 0,
                        CreatedAt TEXT DEFAULT (datetime('now')),
                        LastLoginAt TEXT,
                        IsActive INTEGER DEFAULT 1
                    );
                    
                    CREATE TABLE IF NOT EXISTS Sessions (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        IdentificationNumber TEXT NOT NULL,
                        LoginTime TEXT DEFAULT (datetime('now')),
                        LogoutTime TEXT,
                        SessionToken TEXT NOT NULL,
                        IsActive INTEGER DEFAULT 1
                    );
                    
                    CREATE TABLE IF NOT EXISTS EventLog (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER,
                        IdentificationNumber TEXT,
                        OperationName TEXT NOT NULL,
                        Result TEXT NOT NULL,
                        Description TEXT,
                        Timestamp TEXT DEFAULT (datetime('now')),
                        Severity TEXT DEFAULT 'Info'
                    );
                    
                    CREATE TABLE IF NOT EXISTS Patterns (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Description TEXT,
                        ImageData BLOB,
                        MarginTop INTEGER DEFAULT 10,
                        MarginBottom INTEGER DEFAULT 10,
                        MarginLeft INTEGER DEFAULT 10,
                        MarginRight INTEGER DEFAULT 10,
                        IsActive INTEGER DEFAULT 1,
                        CreatedAt TEXT DEFAULT (datetime('now'))
                    );
                ";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void InsertDefaultData()
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();

                var checkSql = "SELECT COUNT(*) FROM Users WHERE IdentificationNumber = 'admin'";
                using (var cmd = new SqliteCommand(checkSql, conn))
                {
                    if (Convert.ToInt64(cmd.ExecuteScalar()) == 0)
                    {
                        var salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                        var hash = Convert.ToBase64String(
                            System.Security.Cryptography.SHA256.Create().ComputeHash(
                                System.Text.Encoding.UTF8.GetBytes("admin" + salt)
                            )
                        );

                        var insertSql = "INSERT INTO Users (IdentificationNumber, PasswordHash, Salt, IsAdmin) VALUES ('admin', @hash, @salt, 1)";
                        using (var insertCmd = new SqliteCommand(insertSql, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@hash", hash);
                            insertCmd.Parameters.AddWithValue("@salt", salt);
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }
    }
}