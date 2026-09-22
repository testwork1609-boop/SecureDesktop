using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SecureDesktop.Utils
{
    public static class UiTheme
    {
        // === Paleta ===
        public static readonly Color Primary = Color.FromArgb(16, 185, 129);        // emerald-500
        public static readonly Color PrimaryHover = Color.FromArgb(5, 150, 105);    // emerald-600
        public static readonly Color PrimaryPressed = Color.FromArgb(4, 120, 87);   // emerald-700
        public static readonly Color PrimaryLight = Color.FromArgb(209, 250, 229);  // emerald-100

        public static readonly Color Bg = Color.FromArgb(248, 250, 252);           // slate-50
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceAlt = Color.FromArgb(241, 245, 249);    // slate-100
        public static readonly Color Border = Color.FromArgb(226, 232, 240);       // slate-200
        public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);  // slate-300

        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);     // slate-900
        public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);   // slate-600
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);     // slate-400

        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color DangerHover = Color.FromArgb(220, 38, 38);
        public static readonly Color DangerLight = Color.FromArgb(254, 226, 226);
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);
        public static readonly Color Info = Color.FromArgb(59, 130, 246);
        public static readonly Color InfoHover = Color.FromArgb(37, 99, 235);

        // === Rounded path helper ===
        public static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Nadaje kontrolce "card look" - okrągłe rogi, białe tło, cienka obwódka,
        /// opcjonalny delikatny cień. Wywołać raz na kontrolkę, np. w konstruktorze.
        /// Wymaga BackColor = kolor tła rodzica (żeby okrągłe rogi ładnie wyglądały).
        /// </summary>
        public static void MakeCard(Control ctrl, int radius = 10, bool shadow = true, bool border = true)
        {
            ctrl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, ctrl.Width - 1, ctrl.Height - 1);

                if (shadow)
                {
                    for (int i = 3; i >= 1; i--)
                    {
                        var shRect = new Rectangle(rect.X + 1, rect.Y + i, rect.Width - 2, rect.Height - 1);
                        using (var shPath = RoundedPath(shRect, radius))
                        using (var shBrush = new SolidBrush(Color.FromArgb(6, 0, 0, 0)))
                            g.FillPath(shBrush, shPath);
                    }
                }

                using (var path = RoundedPath(rect, radius))
                using (var brush = new SolidBrush(Surface))
                    g.FillPath(brush, path);

                if (border)
                {
                    using (var path = RoundedPath(rect, radius))
                    using (var pen = new Pen(Border, 1))
                        g.DrawPath(pen, path);
                }
            };

            ctrl.Resize += (s, e) => ctrl.Invalidate();
        }

        /// <summary>
        /// Nadaje kontrolce subtelne zaokrąglenie narożników i wyłącza standardowe
        /// tło (żeby rodzica kolor przebijał). Używać dla kontenerów, nie dla kart.
        /// </summary>
        public static void MakeRounded(Control ctrl, int radius)
        {
            ctrl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, ctrl.Width - 1, ctrl.Height - 1);
                using (var path = RoundedPath(rect, radius))
                using (var brush = new SolidBrush(ctrl.BackColor))
                    g.FillPath(brush, path);
            };
        }
    }

    /// <summary>
    /// Nowoczesny przycisk - zaokrąglone rogi, hover, pressed.
    /// Domyślnie primary (zielony), ale można ustawić własne kolory.
    /// </summary>
    public class RoundedButton : Button
    {
        public int CornerRadius { get; set; }
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color OutlineColor { get; set; }
        public int OutlineThickness { get; set; }
        public bool UseFlatText { get; set; }

        private bool _hover;
        private bool _pressed;

        public RoundedButton()
        {
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            this.BackColor = Color.Transparent;
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.ForeColor = Color.White;
            this.Font = UiFonts.BodyBold;
            this.Cursor = Cursors.Hand;

            CornerRadius = 8;
            NormalColor = UiTheme.Primary;
            HoverColor = UiTheme.PrimaryHover;
            PressedColor = UiTheme.PrimaryPressed;
            OutlineColor = Color.Transparent;
            OutlineThickness = 0;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            Color fill = _pressed ? PressedColor : (_hover ? HoverColor : NormalColor);

            using (var path = UiTheme.RoundedPath(rect, CornerRadius))
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);

            if (OutlineThickness > 0 && OutlineColor.A > 0)
            {
                using (var path = UiTheme.RoundedPath(rect, CornerRadius))
                using (var pen = new Pen(OutlineColor, OutlineThickness))
                    g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, this.Text, this.Font, rect, this.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
