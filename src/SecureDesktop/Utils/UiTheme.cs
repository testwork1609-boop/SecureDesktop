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
        public static readonly Color Primary = Color.FromArgb(16, 185, 129);
        public static readonly Color PrimaryHover = Color.FromArgb(5, 150, 105);
        public static readonly Color PrimaryPressed = Color.FromArgb(4, 120, 87);
        public static readonly Color PrimaryLight = Color.FromArgb(209, 250, 229);

        public static readonly Color Bg = Color.FromArgb(248, 250, 252);
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceAlt = Color.FromArgb(241, 245, 249);
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);

        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

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
        /// Nadaje kontrolce "card look" - okrągłe rogi, białe tło, cienka obwódka.
        /// </summary>
        public static void MakeCard(Control ctrl, int radius = 10, bool shadow = true, bool border = true)
        {
            ctrl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var rect = new Rectangle(0, 0, ctrl.Width - 1, ctrl.Height - 1);

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
    }

    /// <summary>
    /// Nowoczesny przycisk z zaokrąglonymi rogami.
    /// Do przycięcia używa Region — żadnych artefaktów na krawędziach.
    /// </summary>
    public class RoundedButton : Button
    {
        public int CornerRadius { get; set; }
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color OutlineColor { get; set; }
        public int OutlineThickness { get; set; }
        public Padding ButtonPadding { get; set; }

        private bool _hover;
        private bool _pressed;

        public RoundedButton()
        {
            // Kluczowe: UserPaint + AllPaintingInWmPaint => WinForms nie rysuje tła
            // ani ramki Win32 - tylko nasz OnPaint. Razem z Region daje czysty efekt.
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.UseVisualStyleBackColor = false;
            this.Cursor = Cursors.Hand;
            this.Font = UiFonts.BodyBold;
            this.ForeColor = Color.White;
            this.BackColor = UiTheme.Primary; // wypełniane w OnPaint, ale musi być nieprzezroczyste

            CornerRadius = 10;
            NormalColor = UiTheme.Primary;
            HoverColor = UiTheme.PrimaryHover;
            PressedColor = UiTheme.PrimaryPressed;
            OutlineColor = Color.Transparent;
            OutlineThickness = 0;
            ButtonPadding = new Padding(16, 0, 16, 0);
            TextAlign = ContentAlignment.MiddleCenter;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        /// <summary>
        /// Przytnij kontrolkę do zaokrąglonego kształtu.
        /// Dzięki temu WinForms nigdy nie rysuje prostokątnych rogów.
        /// </summary>
        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            try
            {
                using (var path = UiTheme.RoundedPath(new Rectangle(0, 0, Width, Height), CornerRadius))
                {
                    var old = Region;
                    Region = new Region(path);
                    if (old != null) old.Dispose();
                }
            }
            catch { }
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

            // Wypełnienie (Region już przycina do zaokrąglenia).
            Color fill = _pressed ? PressedColor : (_hover ? HoverColor : NormalColor);
            using (var brush = new SolidBrush(fill))
                g.FillRectangle(brush, 0, 0, Width, Height);

            // Opcjonalna obwódka (rysowana 1 px w środku).
            if (OutlineThickness > 0 && OutlineColor.A > 0)
            {
                var inset = new Rectangle(
                    OutlineThickness / 2,
                    OutlineThickness / 2,
                    Width - OutlineThickness - 1,
                    Height - OutlineThickness - 1);
                using (var path = UiTheme.RoundedPath(inset, Math.Max(2, CornerRadius - 1)))
                using (var pen = new Pen(OutlineColor, OutlineThickness))
                    g.DrawPath(pen, path);
            }

            // Tekst z paddingiem i wyrównaniem.
            var textRect = new Rectangle(
                ButtonPadding.Left,
                0,
                Math.Max(0, Width - ButtonPadding.Left - ButtonPadding.Right),
                Height);

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            switch (TextAlign)
            {
                case ContentAlignment.MiddleLeft: flags |= TextFormatFlags.Left; break;
                case ContentAlignment.MiddleRight: flags |= TextFormatFlags.Right; break;
                default: flags |= TextFormatFlags.HorizontalCenter; break;
            }

            TextRenderer.DrawText(g, Text, Font, textRect, ForeColor, flags);
        }
    }
}
