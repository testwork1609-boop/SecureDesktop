using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SecureDesktop.Utils
{
    public static class UiTheme
    {
        public static readonly Color Primary = Color.FromArgb(16, 185, 129);
        public static readonly Color PrimaryHover = Color.FromArgb(5, 150, 105);
        public static readonly Color PrimaryPressed = Color.FromArgb(4, 120, 87);

        public static readonly Color PrimarySoft = Color.FromArgb(236, 253, 245);
        public static readonly Color PrimarySoftHover = Color.FromArgb(209, 250, 229);
        public static readonly Color PrimarySoftPressed = Color.FromArgb(167, 243, 208);
        public static readonly Color PrimarySoftText = Color.FromArgb(4, 120, 87);
        public static readonly Color PrimaryLight = Color.FromArgb(209, 250, 229);

        public static readonly Color Info = Color.FromArgb(59, 130, 246);
        public static readonly Color InfoHover = Color.FromArgb(37, 99, 235);
        public static readonly Color InfoSoft = Color.FromArgb(239, 246, 255);
        public static readonly Color InfoSoftHover = Color.FromArgb(219, 234, 254);
        public static readonly Color InfoSoftPressed = Color.FromArgb(191, 219, 254);
        public static readonly Color InfoSoftText = Color.FromArgb(37, 99, 235);

        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color DangerHover = Color.FromArgb(220, 38, 38);
        public static readonly Color DangerLight = Color.FromArgb(254, 226, 226);
        public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);
        public static readonly Color DangerSoftHover = Color.FromArgb(254, 226, 226);
        public static readonly Color DangerSoftPressed = Color.FromArgb(254, 202, 202);
        public static readonly Color DangerSoftText = Color.FromArgb(220, 38, 38);

        public static readonly Color Bg = Color.FromArgb(248, 250, 252);
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);
        public static readonly Color SurfaceAlt = Color.FromArgb(241, 245, 249);
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);

        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);
        public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

        public static readonly Color Warning = Color.FromArgb(245, 158, 11);

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
    /// Nowoczesny przycisk. Klawiatura Enter/Space NIE triggeruje kliku
    /// (to powodowało, że po wpisaniu hasła i Enter, focus wracał do Dashboardu
    /// i „przechwycony" klawisz Enter ponownie aktywował blokadę).
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

        private bool _hover, _pressed;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = UiFonts.BodyBold;
            ForeColor = Color.White;
            TabStop = false;   // nie łapie focusa klawiaturą - eliminuje źródło buga
            CornerRadius = 8;
            NormalColor = UiTheme.Primary;
            HoverColor = UiTheme.PrimaryHover;
            PressedColor = UiTheme.PrimaryPressed;
            OutlineColor = Color.Transparent;
            OutlineThickness = 0;
            ButtonPadding = new Padding(16, 0, 16, 0);
        }

        public static RoundedButton Primary(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text, Size = new Size(width, height), CornerRadius = 8,
                NormalColor = UiTheme.Primary, HoverColor = UiTheme.PrimaryHover,
                PressedColor = UiTheme.PrimaryPressed, ForeColor = Color.White, Font = UiFonts.BodyBold
            };
        }
        public static RoundedButton SoftGreen(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text, Size = new Size(width, height), CornerRadius = 8,
                NormalColor = UiTheme.PrimarySoft, HoverColor = UiTheme.PrimarySoftHover,
                PressedColor = UiTheme.PrimarySoftPressed, ForeColor = UiTheme.PrimarySoftText, Font = UiFonts.BodyBold
            };
        }
        public static RoundedButton SoftRed(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text, Size = new Size(width, height), CornerRadius = 8,
                NormalColor = UiTheme.DangerSoft, HoverColor = UiTheme.DangerSoftHover,
                PressedColor = UiTheme.DangerSoftPressed, ForeColor = UiTheme.DangerSoftText, Font = UiFonts.BodyBold
            };
        }
        public static RoundedButton SoftBlue(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text, Size = new Size(width, height), CornerRadius = 8,
                NormalColor = UiTheme.InfoSoft, HoverColor = UiTheme.InfoSoftHover,
                PressedColor = UiTheme.InfoSoftPressed, ForeColor = UiTheme.InfoSoftText, Font = UiFonts.BodyBold
            };
        }
        public static RoundedButton Ghost(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text, Size = new Size(width, height), CornerRadius = 8,
                NormalColor = UiTheme.Surface, HoverColor = UiTheme.SurfaceAlt,
                PressedColor = UiTheme.Border, ForeColor = UiTheme.TextSecondary, Font = UiFonts.Body,
                OutlineColor = UiTheme.BorderStrong, OutlineThickness = 1
            };
        }
        public static RoundedButton GhostRed(string text, int width, int height)
        {
            return new RoundedButton
            {
                Text = text, Size = new Size(width, height), CornerRadius = 8,
                NormalColor = UiTheme.Surface, HoverColor = UiTheme.DangerSoft,
                PressedColor = UiTheme.DangerSoftHover, ForeColor = UiTheme.DangerSoftText, Font = UiFonts.Body
            };
        }

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
                var inset = new Rectangle(OutlineThickness / 2, OutlineThickness / 2,
                    Width - OutlineThickness - 1, Height - OutlineThickness - 1);
                using (var path = UiTheme.RoundedPath(inset, Math.Max(2, CornerRadius - 1)))
                using (var pen = new Pen(OutlineColor, OutlineThickness))
                    g.DrawPath(pen, path);
            }

            var textRect = new Rectangle(ButtonPadding.Left, 0,
                Math.Max(0, Width - ButtonPadding.Left - ButtonPadding.Right), Height);

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
            if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = false; Invalidate(); }
            base.OnMouseUp(e);
        }

        // UWAGA: celowo NIE nadpisujemy OnKeyDown/OnKeyUp.
        // Poprzednio Enter/Space na focusowanym przycisku wywoływał OnClick,
        // co powodowało że "przechwycony" Enter z dialogu hasła ponownie
        // aktywował blokadę ekranu po powrocie focusu do Dashboardu.
    }

    public class CloseButton : Control
    {
        private bool _hover;
        public Color HoverBack { get; set; }
        public Color IconColor { get; set; }
        public Color HoverIconColor { get; set; }

        public CloseButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
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

    public class LanguageButton : Control
    {
        public string LangCode { get; set; }
        private bool _selected;
        private bool _hover;

        public bool IsSelected
        {
            get { return _selected; }
            set { _selected = value; Invalidate(); }
        }

        public LanguageButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(48, 30);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            if (_selected)
            {
                using (var path = UiTheme.RoundedPath(rect, 6))
                using (var pen = new Pen(UiTheme.Primary, 2))
                    g.DrawPath(pen, path);
            }
            else if (_hover)
            {
                using (var path = UiTheme.RoundedPath(rect, 6))
                using (var pen = new Pen(UiTheme.BorderStrong, 1))
                    g.DrawPath(pen, path);
            }

            var inner = new Rectangle(6, 6, Width - 12, Height - 12);
            if (inner.Width <= 0 || inner.Height <= 0) return;

            if (LangCode == "pl") DrawPolishFlag(g, inner);
            else DrawBritishFlag(g, inner);
        }

        private static void DrawPolishFlag(Graphics g, Rectangle rect)
        {
            int half = rect.Height / 2;
            using (var b = new SolidBrush(Color.White))
                g.FillRectangle(b, rect.X, rect.Y, rect.Width, half);
            using (var b = new SolidBrush(Color.FromArgb(220, 20, 60)))
                g.FillRectangle(b, rect.X, rect.Y + half, rect.Width, rect.Height - half);
            using (var pen = new Pen(UiTheme.Border, 1))
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        }

        private static void DrawBritishFlag(Graphics g, Rectangle rect)
        {
            int w = rect.Width, h = rect.Height;

            using (var b = new SolidBrush(Color.FromArgb(1, 33, 105)))
                g.FillRectangle(b, rect);

            using (var pen = new Pen(Color.White, Math.Max(3, h / 5f)))
            {
                g.DrawLine(pen, rect.X, rect.Y, rect.Right, rect.Bottom);
                g.DrawLine(pen, rect.Right, rect.Y, rect.X, rect.Bottom);
            }

            int crossW = Math.Max(3, w / 5);
            int crossH = Math.Max(3, h / 5);
            using (var b = new SolidBrush(Color.White))
            {
                g.FillRectangle(b, rect.X + (w - crossW) / 2, rect.Y, crossW, h);
                g.FillRectangle(b, rect.X, rect.Y + (h - crossH) / 2, w, crossH);
            }

            using (var pen = new Pen(Color.FromArgb(200, 16, 46), Math.Max(2, h / 10f)))
            {
                g.DrawLine(pen, rect.X, rect.Y, rect.Right, rect.Bottom);
                g.DrawLine(pen, rect.Right, rect.Y, rect.X, rect.Bottom);
            }

            int rcw = Math.Max(2, w / 11);
            int rch = Math.Max(2, h / 11);
            using (var b = new SolidBrush(Color.FromArgb(200, 16, 46)))
            {
                g.FillRectangle(b, rect.X + (w - rcw) / 2, rect.Y, rcw, h);
                g.FillRectangle(b, rect.X, rect.Y + (h - rch) / 2, w, rch);
            }

            using (var pen = new Pen(UiTheme.Border, 1))
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        }
    }
}
