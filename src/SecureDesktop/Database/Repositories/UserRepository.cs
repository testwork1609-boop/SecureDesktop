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

        public User GetById(int id)
        {
            return _db.GetData().Users.FirstOrDefault(u => u.Id == id);
        }

        public void AddUser(User user)
        {
            var data = _db.GetData();
            user.Id = data.NextUserId++;
            user.CreatedAt = DateTime.Now;
            user.IsActive = true;
            if (user.IsHeadAdmin) user.IsAdmin = true;
            data.Users.Add(user);
            _db.Save();
        }

        public void UpdateUser(User user)
        {
            var data = _db.GetData();
            var existing = data.Users.FirstOrDefault(u => u.Id == user.Id);
            if (existing != null)
            {
                existing.IdentificationNumber = user.IdentificationNumber;
                existing.IsAdmin = user.IsAdmin;
                existing.IsHeadAdmin = user.IsHeadAdmin;
                existing.IsActive = user.IsActive;
                existing.ShiftId = user.ShiftId;
                existing.FirstName = user.FirstName;
                existing.LastName = user.LastName;
                existing.DisplayName = user.DisplayName;

                // Hasło indywidualne
                existing.UseIndividualPassword = user.UseIndividualPassword;
                existing.PasswordHash = user.PasswordHash;
                existing.Salt = user.Salt;

                if (existing.IsHeadAdmin) existing.IsAdmin = true;
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
