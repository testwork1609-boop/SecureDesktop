using System;
using System.Data.SQLite;
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
            using (var conn = new SQLiteConnection(_connectionString))
            {
                return conn.QuerySingle<int>(@"
                    INSERT INTO Sessions (UserId, IdentificationNumber, LoginTime, SessionToken, IsActive)
                    VALUES (@UserId, @IdNumber, @LoginTime, @Token, 1);
                    SELECT last_insert_rowid()",
                    new { session.UserId, IdNumber = session.IdentificationNumber, 
                          session.LoginTime, Token = session.SessionToken });
            }
        }

        public void EndSession(int sessionId)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Execute(@"
                    UPDATE Sessions SET LogoutTime = @Now, IsActive = 0 
                    WHERE Id = @Id",
                    new { Now = DateTime.Now, Id = sessionId });
            }
        }
    }
}