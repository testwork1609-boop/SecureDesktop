using System;

namespace SecureDesktop.Models
{
    public class EventLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string IdentificationNumber { get; set; }
        public string OperationName { get; set; }
        public string Result { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; } = "Info";
    }
}