using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SecureDesktop.Models;

namespace SecureDesktop.Database.Repositories
{
    public class UserRepository
    {
        private readonly DatabaseInitializer _db;

        public UserRepository(DatabaseInitializer db)
        {
            _db = db;
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
            try
            {
                var data = _db.GetData();
                user.Id = data.Users.Count > 0 ? data.Users.Max(u => u.Id) + 1 : 1;
                user.CreatedAt = DateTime.Now;
                user.IsActive = true;
                data.Users.Add(user);
                _db.Save();

                // Diagnostyka
                string json = File.ReadAllText(_db.GetDatabasePath());
                bool found = json.Contains(user.IdentificationNumber);

                MessageBox.Show(
                    $"Dodano użytkownika: {user.IdentificationNumber}\n" +
                    $"Zapisano w pliku: {(found ? "TAK ✅" : "NIE ❌")}\n\n" +
                    $"Ścieżka pliku:\n{_db.GetDatabasePath()}",
                    "Diagnostyka UserRepository",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas dodawania użytkownika:\n{ex}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public void UpdateUser(User user)
        {
            var data = _db.GetData();
            var existing = data.Users.FirstOrDefault(u => u.Id == user.Id);
            if (existing != null)
            {
                existing.IdentificationNumber = user.IdentificationNumber;
                existing.IsAdmin = user.IsAdmin;
                existing.IsActive = user.IsActive;
                _db.Save();
            }
        }

        public void DeleteUser(int userId)
        {
            var data = _db.GetData();
            var user = data.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.IsActive = false;
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