using System;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class EventLogRepository
    {
        private readonly DatabaseInitializer _db;

        public EventLogRepository(DatabaseInitializer db)
        {
            _db = db;
        }

        public void Create(EventLog entry)
        {
            var data = _db.GetData();
            entry.Id = data.NextEventId++;
            entry.Timestamp = DateTime.Now;
            data.EventLogs.Add(entry);
            _db.Save();
        }
    }
}