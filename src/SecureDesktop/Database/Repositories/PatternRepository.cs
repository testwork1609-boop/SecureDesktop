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

        public List<Pattern> GetAllPatterns()
        {
            return _db.GetData().Patterns;
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

        public void Delete(int id)
        {
            var data = _db.GetData();
            var pattern = data.Patterns.FirstOrDefault(p => p.Id == id);
            if (pattern != null)
            {
                data.Patterns.Remove(pattern);
                _db.Save();
            }
        }

        public void Update(Pattern pattern)
        {
            var data = _db.GetData();
            var existing = data.Patterns.FirstOrDefault(p => p.Id == pattern.Id);
            if (existing != null)
            {
                existing.Name = pattern.Name;
                existing.Description = pattern.Description;
                existing.MarginTop = pattern.MarginTop;
                existing.MarginBottom = pattern.MarginBottom;
                existing.MarginLeft = pattern.MarginLeft;
                existing.MarginRight = pattern.MarginRight;
                existing.IsActive = pattern.IsActive;
                existing.UpdatedAt = System.DateTime.Now;
                _db.Save();
            }
        }
    }
}