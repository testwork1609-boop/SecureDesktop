using System.Collections.Generic;
using System.Data.SQLite;
using Dapper;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class PatternRepository
    {
        private readonly string _connectionString;

        public PatternRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IEnumerable<Pattern> GetActivePatterns()
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                return conn.Query<Pattern>("SELECT * FROM Patterns WHERE IsActive = 1");
            }
        }

        public int Create(Pattern pattern)
        {
            using (var conn = new SQLiteConnection(_connectionString))
            {
                return conn.QuerySingle<int>(@"
                    INSERT INTO Patterns (Name, Description, ImageData, MarginTop, MarginBottom, MarginLeft, MarginRight, IsActive, CreatedAt)
                    VALUES (@Name, @Description, @ImageData, @MarginTop, @MarginBottom, @MarginLeft, @MarginRight, @IsActive, @CreatedAt);
                    SELECT last_insert_rowid()",
                    pattern);
            }
        }
    }
}