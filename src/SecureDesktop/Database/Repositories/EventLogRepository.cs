using System.Collections.Generic;
using System.Data.SQLite;
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
            using (var conn = new SQLiteConnection(_connectionString))
            {
                conn.Execute(@"
                    INSERT INTO EventLog (UserId, IdentificationNumber, OperationName, Result, Description, Timestamp, Severity)
                    VALUES (@UserId, @IdNumber, @Operation, @Result, @Description, @Timestamp, @Severity)",
                    new { entry.UserId, IdNumber = entry.IdentificationNumber, 
                          Operation = entry.OperationName, entry.Result, 
                          entry.Description, entry.Timestamp, entry.Severity });
            }
        }

        public IEnumerable<EventLog> GetAll()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                return conn.Query<EventLog>("SELECT * FROM EventLog ORDER BY Timestamp DESC LIMIT 1000");
            }
        }
    }
}