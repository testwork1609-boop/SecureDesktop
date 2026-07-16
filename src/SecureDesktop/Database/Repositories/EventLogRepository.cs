using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
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

            // Diagnostyka przed Save()
            int beforeSave = data.Users.Count;
            _db.Save();

            // Diagnostyka po Save() – sprawdź, czy użytkownicy nie zginęli
            string json = File.ReadAllText(_db.GetDatabasePath());
            var checkData = JsonConvert.DeserializeObject<DatabaseData>(json);
            int afterSave = checkData?.Users?.Count ?? 0;
            string userList = string.Join(", ", checkData?.Users?.Select(u => u.IdentificationNumber) ?? new List<string>());

            MessageBox.Show(
                $"EventLog Create: {entry.OperationName}\n" +
                $"Użytkownicy przed zapisem: {beforeSave}\n" +
                $"Użytkownicy po zapisie: {afterSave}\n" +
                $"Lista: {userList}\n\n" +
                $"Ścieżka: {_db.GetDatabasePath()}",
                "Diagnostyka EventLog",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}