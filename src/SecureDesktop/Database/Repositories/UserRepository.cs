using System;
using System.Collections.Generic;
using System.Linq;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class UserRepository
    {
        private readonly DatabaseInitializer _db;

        public UserRepository(DatabaseInitializer db)
        {
            _db = db;
            public string GetDatabasePath() => _dbPath;
        }

        public User GetByIdentificationNumber(string id)
        {
            return _db.GetData().Users
                .FirstOrDefault(u => u.IdentificationNumber == id && u.IsActive);
        }

        public List<User> GetAllUsers()
        {
            return _db.GetData().Users.ToList();
        }

        public void AddUser(User user)
        {
            var data = _db.GetData();
            user.Id = data.Users.Count > 0 ? data.Users.Max(u => u.Id) + 1 : 1;
            user.CreatedAt = DateTime.Now;
            user.IsActive = true;
            data.Users.Add(user);
            _db.Save();
        }

       public void AddUser(User user)
{
    try
    {
        var data = _db.GetData();
        user.Id = data.Users.Count > 0 ? data.Users.Max(u => u.Id) + 1 : 1;
        user.CreatedAt = DateTime.Now;
        user.IsActive = true;
        data.Users.Add(user);
        _db.Save();   // <- zapis do pliku

        // 🔍 DIAGNOSTYKA
        string check = File.ReadAllText(_db.GetDatabasePath());  // potrzebujemy metody zwracającej ścieżkę
        bool found = check.Contains(user.IdentificationNumber);
        MessageBox.Show(
            $"Dodano użytkownika: {user.IdentificationNumber}\n" +
            $"Zapisano w pliku: {(found ? "TAK" : "NIE")}\n" +
            $"Ścieżka: {_db.GetDatabasePath()}",
            "DEBUG AddUser");
    }
    catch (Exception ex)
    {
        MessageBox.Show("Błąd zapisu użytkownika:\n" + ex.ToString(), "BŁĄD");
    }
}

        public void DeleteUser(int userId)
        {
            var data = _db.GetData();
            var user = data.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.IsActive = false; // dezaktywacja zamiast usuwania
                _db.Save();
            }
        }

        public void UpdateLastLogin(int userId)
        {
            var user = _db.GetData().Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.Now;
                _db.Save();
            }
        }
    }
}