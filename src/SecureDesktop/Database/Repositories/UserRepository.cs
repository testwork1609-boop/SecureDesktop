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

        public void UpdateLastLogin(int userId)
        {
            var user = _db.GetData().Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.LastLoginAt = System.DateTime.Now;
                _db.Save();
            }
        }
    }
}