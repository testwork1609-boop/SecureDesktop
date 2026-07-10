using System;
using System.Linq;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class SessionRepository
    {
        private readonly DatabaseInitializer _db;

        public SessionRepository(DatabaseInitializer db)
        {
            _db = db;
        }

        public int Create(Session session)
        {
            var data = _db.GetData();
            session.Id = data.NextSessionId++;
            session.LoginTime = DateTime.Now;
            session.IsActive = true;
            data.Sessions.Add(session);
            _db.Save();
            return session.Id;
        }

        public void EndSession(int sessionId)
        {
            var session = _db.GetData().Sessions.FirstOrDefault(s => s.Id == sessionId);
            if (session != null)
            {
                session.LogoutTime = DateTime.Now;
                session.IsActive = false;
                _db.Save();
            }
        }
    }
}