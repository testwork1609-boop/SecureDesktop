using System;

namespace SecureDesktop.Models
{
    public class Pattern
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public byte[] ImageData { get; set; }
        public int MarginTop { get; set; } = 10;
        public int MarginBottom { get; set; } = 10;
        public int MarginLeft { get; set; } = 10;
        public int MarginRight { get; set; } = 10;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}