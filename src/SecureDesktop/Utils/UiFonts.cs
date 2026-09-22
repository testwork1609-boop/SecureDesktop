using System.Drawing;

namespace SecureDesktop.Utils
{
    /// <summary>
    /// Współdzielone, statyczne instancje Font używane w całej aplikacji.
    /// Skala typograficzna: Display / H1 / H2 / H3 / Body / Caption / Small.
    /// </summary>
    public static class UiFonts
    {
        private const string Family = "Segoe UI";

        // Nagłówki
        public static readonly Font Display = new Font(Family, 24, FontStyle.Bold);
        public static readonly Font H1 = new Font(Family, 18, FontStyle.Bold);
        public static readonly Font H2 = new Font(Family, 14, FontStyle.Bold);
        public static readonly Font H3 = new Font(Family, 12, FontStyle.Bold);

        // Treść
        public static readonly Font Body = new Font(Family, 10, FontStyle.Regular);
        public static readonly Font BodyBold = new Font(Family, 10, FontStyle.Bold);
        public static readonly Font BodyLarge = new Font(Family, 11, FontStyle.Regular);
        public static readonly Font BodyLargeBold = new Font(Family, 11, FontStyle.Bold);

        // Drobne
        public static readonly Font Caption = new Font(Family, 9, FontStyle.Regular);
        public static readonly Font CaptionBold = new Font(Family, 9, FontStyle.Bold);
        public static readonly Font Small = new Font(Family, 8, FontStyle.Regular);
        public static readonly Font SmallBold = new Font(Family, 8, FontStyle.Bold);

        // Mono
        public static readonly Font Mono = new Font("Consolas", 10, FontStyle.Regular);
        public static readonly Font MonoSmall = new Font("Consolas", 9, FontStyle.Regular);

        // Aliasy wsteczne (żeby stare odwołania nie padały)
        public static readonly Font Segoe9 = Caption;
        public static readonly Font Segoe9Bold = CaptionBold;
        public static readonly Font Segoe10 = Body;
        public static readonly Font Segoe10Bold = BodyBold;
        public static readonly Font Segoe11 = BodyLarge;
        public static readonly Font Segoe11Bold = BodyLargeBold;
        public static readonly Font Segoe12 = new Font(Family, 12, FontStyle.Regular);
        public static readonly Font Segoe12Bold = H3;
        public static readonly Font Segoe14Bold = H2;
        public static readonly Font Segoe16Bold = new Font(Family, 16, FontStyle.Bold);
        public static readonly Font Segoe18Bold = H1;
        public static readonly Font Segoe22Bold = new Font(Family, 22, FontStyle.Bold);
        public static readonly Font Segoe24 = new Font(Family, 24, FontStyle.Regular);
        public static readonly Font Segoe36 = new Font(Family, 36, FontStyle.Regular);
        public static readonly Font Consolas9 = MonoSmall;
        public static readonly Font Consolas10 = Mono;
    }
}
