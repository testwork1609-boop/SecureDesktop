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

        /// <summary>
        /// Próg zgodności (0.0 - 1.0) wymagany, aby ten konkretny wzorzec
        /// został uznany za znaleziony na ekranie. Każdy wzorzec może mieć
        /// inną charakterystykę wizualną (kontrast, jednolitość kolorów),
        /// więc jeden globalny próg dla wszystkich wzorców powodował, że
        /// część z nich (np. drugi dodany wzorzec) nigdy nie osiągała progu,
        /// mimo że pierwszy działał bez problemu.
        /// </summary>
        public double MatchThreshold { get; set; } = 0.75;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
