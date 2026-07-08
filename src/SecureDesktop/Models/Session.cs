using System;

namespace SecureDesktop.Models
{
    public class Session
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string IdentificationNumber { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; }
        public string SessionToken { get; set; }
        public bool IsActive { get; set; } = true;
    }
}