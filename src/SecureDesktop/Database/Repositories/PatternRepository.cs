using System.Collections.Generic;
using System.Linq;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class PatternRepository
    {
        private readonly DatabaseInitializer _db;

        public PatternRepository(DatabaseInitializer db)
        {
            _db = db;
        }

        public IEnumerable<Pattern> GetActivePatterns()
        {
            return _db.GetData().Patterns.Where(p => p.IsActive);
        }

        public int Create(Pattern pattern)
        {
            var data = _db.GetData();
            pattern.Id = data.NextPatternId++;
            pattern.CreatedAt = System.DateTime.Now;
            data.Patterns.Add(pattern);
            _db.Save();
            return pattern.Id;
        }
    }
}