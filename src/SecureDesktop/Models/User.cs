using System;

namespace SecureDesktop.Models
{
    public class User
    {
        public int Id { get; set; }
        public string IdentificationNumber { get; set; }
        public string PasswordHash { get; set; }
        public string Salt { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}