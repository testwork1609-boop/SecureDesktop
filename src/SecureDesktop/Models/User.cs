using System;

namespace SecureDesktop.Models
{
    public class User
    {
        public int Id { get; set; }

        /// <summary>
        /// Login użytkownika — w praktyce PIN (ciąg cyfr/liter).
        /// </summary>
        public string IdentificationNumber { get; set; }

        public string PasswordHash { get; set; }
        public string Salt { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Id zmiany, do której należy użytkownik. Hasło logowania pobierane
        /// jest z tej zmiany (Shift.PasswordHash + Shift.Salt).
        /// </summary>
        public int? ShiftId { get; set; }

        // === Dane z importu XLSX (opcjonalne) ===

        public string FirstName { get; set; }
        public string LastName { get; set; }

        /// <summary>
        /// Maskowana nazwa do wyświetlania, generowana przy imporcie XLSX:
        /// pierwsze 2 litery imienia + "**" + pierwsze 2 litery nazwiska + "**"
        /// np. "Jan Kowalski" → "Ja**Ko**".
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Zwraca DisplayName gdy jest ustawiony, inaczej PIN.
        /// Używane wszędzie gdzie chcemy pokazać użytkownika ("Ja**Ko**" zamiast "1234").
        /// </summary>
        public string DisplayNameOrPin
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayName)) return DisplayName;
                return IdentificationNumber ?? "";
            }
        }
    }
}
