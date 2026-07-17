using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Zwraca wszystkie zdarzenia posortowane od najnowszych.
        /// </summary>
        public List<EventLog> GetAll()
        {
            var data = _db.GetData();
            if (data?.EventLogs == null) return new List<EventLog>();

            return data.EventLogs
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Zwraca ostatnie N zdarzen (od najnowszych).
        /// </summary>
        public List<EventLog> GetRecent(int count)
        {
            return GetAll().Take(count).ToList();
        }
    }
}