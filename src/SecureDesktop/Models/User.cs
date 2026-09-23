using System;

namespace SecureDesktop.Models
{
    public class User
    {
        public int Id { get; set; }
        public string IdentificationNumber { get; set; }

        /// <summary>
        /// Hash hasła indywidualnego. Używany TYLKO gdy
        /// UseIndividualPassword == true. W przeciwnym razie hasło
        /// weryfikowane jest przez Shift.PasswordHash + Shift.Salt.
        /// </summary>
        public string PasswordHash { get; set; }
        public string Salt { get; set; }

        /// <summary>
        /// True = użytkownik ma własne hasło (niezależne od zmiany).
        /// False = użytkownik loguje się hasłem przypisanej zmiany.
        /// </summary>
        public bool UseIndividualPassword { get; set; }

        public bool IsAdmin { get; set; }
        public bool IsHeadAdmin { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        public int? ShiftId { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DisplayName { get; set; }

        public string DisplayNameOrPin
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayName)) return DisplayName;
                return IdentificationNumber ?? "";
            }
        }

        public string RoleName
        {
            get
            {
                if (IsHeadAdmin) return "HeadAdmin";
                if (IsAdmin) return "Admin";
                return "User";
            }
        }
    }
}
