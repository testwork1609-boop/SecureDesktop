using System;
using Microsoft.Data.Sqlite;
using Dapper;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class EventLogRepository
    {
        private readonly string _connectionString;

        public EventLogRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Create(EventLog entry)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Execute(@"
                    INSERT INTO EventLog (UserId, IdentificationNumber, OperationName, Result, Description, Timestamp, Severity)
                    VALUES (@UserId, @IdNumber, @Operation, @Result, @Description, datetime('now'), @Severity)",
                    new { entry.UserId, IdNumber = entry.IdentificationNumber, Operation = entry.OperationName, 
                          entry.Result, entry.Description, entry.Severity });
            }
        }
    }
}