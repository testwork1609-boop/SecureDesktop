using System;
using Microsoft.Data.Sqlite;
using Dapper;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class SessionRepository
    {
        private readonly string _connectionString;

        public SessionRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public int Create(Session session)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                return conn.QuerySingle<int>(@"
                    INSERT INTO Sessions (UserId, IdentificationNumber, LoginTime, SessionToken, IsActive)
                    VALUES (@UserId, @IdNumber, datetime('now'), @Token, 1);
                    SELECT last_insert_rowid()",
                    new { session.UserId, IdNumber = session.IdentificationNumber, Token = session.SessionToken });
            }
        }
    }
}