using System.Drawing;

namespace SecureDesktop.Utils
{
    /// <summary>
    /// Współdzielone, statyczne instancje Font używane w całej aplikacji.
    /// Font implementuje IDisposable i trzyma uchwyt GDI - tworzenie nowego
    /// obiektu Font przy każdym otwarciu formularza (np. ConfigurationForm,
    /// LoginForm w pętli logowania, ScreenLockForm przy każdej blokadzie)
    /// bez jego zwalniania prowadzi do powolnego wyczerpywania zasobów GDI
    /// systemu Windows. Fonty tutaj żyją przez cały czas działania aplikacji
    /// i są tworzone tylko raz, więc nie ma potrzeby ich zwalniania.
    /// </summary>
    public static class UiFonts
    {
        public static readonly Font Segoe9 = new Font("Segoe UI", 9);
        public static readonly Font Segoe9Bold = new Font("Segoe UI", 9, FontStyle.Bold);
        public static readonly Font Segoe10 = new Font("Segoe UI", 10);
        public static readonly Font Segoe10Bold = new Font("Segoe UI", 10, FontStyle.Bold);
        public static readonly Font Segoe11 = new Font("Segoe UI", 11);
        public static readonly Font Segoe11Bold = new Font("Segoe UI", 11, FontStyle.Bold);
        public static readonly Font Segoe12 = new Font("Segoe UI", 12);
        public static readonly Font Segoe12Bold = new Font("Segoe UI", 12, FontStyle.Bold);
        public static readonly Font Segoe14Bold = new Font("Segoe UI", 14, FontStyle.Bold);
        public static readonly Font Segoe16Bold = new Font("Segoe UI", 16, FontStyle.Bold);
        public static readonly Font Segoe18Bold = new Font("Segoe UI", 18, FontStyle.Bold);
        public static readonly Font Segoe22Bold = new Font("Segoe UI", 22, FontStyle.Bold);
        public static readonly Font Segoe24 = new Font("Segoe UI", 24);
        public static readonly Font Segoe36 = new Font("Segoe UI", 36);
        public static readonly Font Consolas9 = new Font("Consolas", 9);
        public static readonly Font Consolas10 = new Font("Consolas", 10);
    }
}