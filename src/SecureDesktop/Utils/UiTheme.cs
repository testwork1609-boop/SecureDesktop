using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SecureDesktop.Utils
{
    public static class UiTheme
    {
        // === Primary (solid) ===
        public static readonly Color Primary = Color.FromArgb(16, 185, 129);       // emerald-500
        public static readonly Color PrimaryHover = Color.FromArgb(5, 150, 105);   // emerald-600
        public static readonly Color PrimaryPressed = Color.FromArgb(4, 120, 87);  // emerald-700

        // === Primary soft (ghost w tonacji zielonej) ===
        public static readonly Color PrimarySoft = Color.FromArgb(236, 253, 245);
        public static readonly Color PrimarySoftHover = Color.FromArgb(209, 250, 229);
        public static readonly Color PrimarySoftPressed = Color.FromArgb(167, 243, 208);
        public static readonly Color PrimarySoftText = Color.FromArgb(4, 120, 87);

        // === Info (niebieski) ===
        public static readonly Color Info = Color.FromArgb(59, 130, 246);
        public static readonly Color InfoHover = Color.FromArgb(37, 99, 235);

        // === Info soft (ghost niebieski) ===
        public static readonly Color InfoSoft = Color.FromArgb(239, 246, 255);
        public static readonly Color InfoSoftHover = Color.FromArgb(219, 234, 254);
        public static readonly Color InfoSoftPressed = Color.FromArgb(191, 219, 254);
        public static readonly Color InfoSoftText = Color.FromArgb(37, 99, 235);

        // === Danger ===
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color DangerHover = Color.FromArgb(220, 38, 38);
        public static readonly Color DangerLight = Color.FromArgb(254, 226, 226);

        // === Danger soft (ghost czerwony) ===
        public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);
        public static readonly Color DangerSoftHover = Color.FromArgb(254, 226, 226);
        public static readonly Color DangerSoftPressed = Color.FromArgb(254, 202, 202);
        public static readonly Color DangerSoftText = Color.FromArgb(220, 38, 38);

        // === Powierzchnie ===
        public static readonly Color Bg = Color.FromArgb(248, 250, 252);
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceAlt = Color.FromArgb(241, 245, 249);
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);

        // === Tekst ===
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

        public static readonly Color Warning = Color.FromArgb(245, 158, 11);

        // === Rounded path ===
        public static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(rect); return path; }
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
    /// Nowoczesny, subtelny przycisk. Bazuje na Control, pełne rysowanie w OnPaint,
    /// brak halo na krawędziach. Warianty: solid (primary), soft (ghost z tintem),
    /// outline (przezroczysty + cienka obwódka).
    /// </summary>
    public class RoundedButton : Control
    {
        public int CornerRadius { get; set; }
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color OutlineColor { get; set; }
        public int OutlineThickness { get; set; }
        public Padding ButtonPadding { get; set; }
        private ContentAlignment _textAlign = ContentAlignment.MiddleCenter;

        public ContentAlignment TextAlign
        {
            get { return _textAlign; }
            set { _textAlign = value; Invalidate(); }
        }

        private bool _hover;
        private bool _pressed;

        public RoundedButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.Selectable, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = UiFonts.BodyBold;
            ForeColor = Color.White;
            TabStop = true;

            CornerRadius = 8;
            NormalColor = UiTheme.Primary;
            HoverColor = UiTheme.PrimaryHover;
            PressedColor = UiTheme.PrimaryPressed;
            OutlineColor = Color.Transparent;
            OutlineThickness = 0;
            ButtonPadding = new Padding(16, 0, 16, 0);
        }

        // === Fabryki dla typowych wariantów ===

        public static RoundedButton Primary(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text,
                Size = new Size(width, height),
                CornerRadius = 8,
                NormalColor = UiTheme.Primary,
                HoverColor = UiTheme.PrimaryHover,
                PressedColor = UiTheme.PrimaryPressed,
                ForeColor = Color.White,
                Font = UiFonts.BodyBold
            };
        }

        public static RoundedButton SoftGreen(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text,
                Size = new Size(width, height),
                CornerRadius = 8,
                NormalColor = UiTheme.PrimarySoft,
                HoverColor = UiTheme.PrimarySoftHover,
                PressedColor = UiTheme.PrimarySoftPressed,
                ForeColor = UiTheme.PrimarySoftText,
                Font = UiFonts.BodyBold
            };
        }

        public static RoundedButton SoftRed(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text,
                Size = new Size(width, height),
                CornerRadius = 8,
                NormalColor = UiTheme.DangerSoft,
                HoverColor = UiTheme.DangerSoftHover,
                PressedColor = UiTheme.DangerSoftPressed,
                ForeColor = UiTheme.DangerSoftText,
                Font = UiFonts.BodyBold
            };
        }

        public static RoundedButton SoftBlue(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text,
                Size = new Size(width, height),
                CornerRadius = 8,
                NormalColor = UiTheme.InfoSoft,
                HoverColor = UiTheme.InfoSoftHover,
                PressedColor = UiTheme.InfoSoftPressed,
                ForeColor = UiTheme.InfoSoftText,
                Font = UiFonts.BodyBold
            };
        }

        public static RoundedButton Ghost(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text,
                Size = new Size(width, height),
                CornerRadius = 8,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.SurfaceAlt,
                PressedColor = UiTheme.Border,
                ForeColor = UiTheme.TextSecondary,
                Font = UiFonts.Body,
                OutlineColor = UiTheme.BorderStrong,
                OutlineThickness = 1
            };
        }

        public static RoundedButton GhostRed(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text,
                Size = new Size(width, height),
                CornerRadius = 8,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.DangerSoft,
                PressedColor = UiTheme.DangerSoftHover,
                ForeColor = UiTheme.DangerSoftText,
                Font = UiFonts.Body
            };
        }

        // === Rendering ===

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = _pressed ? PressedColor : (_hover ? HoverColor : NormalColor);

            using (var path = UiTheme.RoundedPath(rect, CornerRadius))
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);

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

            var textRect = new Rectangle(
                ButtonPadding.Left,
                0,
                Math.Max(0, Width - ButtonPadding.Left - ButtonPadding.Right),
                Height);

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            switch (_textAlign)
            {
                case ContentAlignment.MiddleLeft: flags |= TextFormatFlags.Left; break;
                case ContentAlignment.MiddleRight: flags |= TextFormatFlags.Right; break;
                default: flags |= TextFormatFlags.HorizontalCenter; break;
            }

            TextRenderer.DrawText(g, Text, Font, textRect, ForeColor, flags);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Focus(); Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = false; Invalidate(); }
            base.OnMouseUp(e);
        }

        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { _pressed = true; Invalidate(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                _pressed = false; Invalidate(); OnClick(EventArgs.Empty); e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    /// <summary>
    /// Samodzielnie rysowany przycisk X (do zamknięcia okna).
    /// </summary>
    public class CloseButton : Control
    {
        private bool _hover;

        public Color HoverBack { get; set; }
        public Color IconColor { get; set; }
        public Color HoverIconColor { get; set; }

        public CloseButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(36, 36);
            HoverBack = UiTheme.DangerSoft;
            IconColor = UiTheme.TextMuted;
            HoverIconColor = UiTheme.Danger;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_hover)
            {
                using (var path = UiTheme.RoundedPath(rect, 8))
                using (var brush = new SolidBrush(HoverBack))
                    g.FillPath(brush, path);
            }

            Color col = _hover ? HoverIconColor : IconColor;
            using (var pen = new Pen(col, 2f))
            {
                int pad = 11;
                g.DrawLine(pen, pad, pad, Width - pad, Height - pad);
                g.DrawLine(pen, Width - pad, pad, pad, Height - pad);
            }
        }
    }
}
