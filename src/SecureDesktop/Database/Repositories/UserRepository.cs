using System;
using Microsoft.Data.Sqlite;
using Dapper;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class UserRepository
    {
        private readonly string _connectionString;

        public UserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public User GetByIdentificationNumber(string id)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                return conn.QuerySingleOrDefault<User>(
                    "SELECT * FROM Users WHERE IdentificationNumber = @Id AND IsActive = 1",
                    new { Id = id });
            }
        }

        public void UpdateLastLogin(int userId)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Execute(
                    "UPDATE Users SET LastLoginAt = datetime('now') WHERE Id = @Id",
                    new { Id = userId });
            }
        }
    }
}