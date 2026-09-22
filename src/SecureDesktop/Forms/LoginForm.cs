using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;
using SecureDesktop.Utils;

namespace SecureDesktop.Forms
{
    public class LoginForm : Form
    {
        private readonly DatabaseInitializer _db;
        private readonly UserRepository _userRepo;
        private readonly SessionRepository _sessionRepo;
        private readonly EventLogRepository _eventRepo;
        private TextBox _idBox;
        private TextBox _passBox;
        private Label _errorLabel;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _idLabel;
        private Label _passLabel;
        private Label _hintLabel;
        private RoundedButton _loginBtn;
        private LanguageButton _langPl;
        private LanguageButton _langGb;

        public User LoggedInUser { get; private set; }
        public int LoggedInSessionId { get; private set; }

        public LoginForm(DatabaseInitializer db)
        {
            _db = db;
            _userRepo = new UserRepository(_db);
            _sessionRepo = new SessionRepository(_db);
            _eventRepo = new EventLogRepository(_db);
            InitializeComponent();
            Loc.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Loc.LanguageChanged -= OnLanguageChanged;
            base.Dispose(disposing);
        }

        private void OnLanguageChanged()
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired) { try { BeginInvoke(new Action(ApplyLanguage)); } catch { } return; }
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            this.Text = Loc.T("login.window_title");
            _titleLabel.Text = Loc.T("dash.header_brand");
            _subtitleLabel.Text = Loc.T("login.subtitle");
            _idLabel.Text = Loc.T("login.id");
            _passLabel.Text = Loc.T("login.password");
            _loginBtn.Text = Loc.T("login.submit");
            _hintLabel.Text = Loc.T("login.hint");
            _langPl.IsSelected = Loc.Current == "pl";
            _langGb.IsSelected = Loc.Current == "en";
        }

        private void InitializeComponent()
        {
            this.Text = "SecureDesktop";
            this.Size = new Size(480, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = UiTheme.Bg;
            this.Icon = Program.AppIcon;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            this.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = UiTheme.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 14))
                    this.Region = new Region(path);
            };

            // ========================================================
            // Kolejność dodawania kontrolek ma znaczenie (WinForms):
            // ostatnio dodana = na wierzchu. Dlatego najpierw dodajemy
            // elementy, które mają być NA SPODZIE, a na końcu te,
            // które mają być NA WIERZCHU (flagi, X).
            // ========================================================

            // 1) CARD (najniżej) — główna karta logowania
            var card = new Panel
            {
                Location = new Point(44, 80),
                Size = new Size(392, 520),
                BackColor = UiTheme.Bg
            };
            UiTheme.MakeCard(card, 14, true, true);
            this.Controls.Add(card);

            var logoCircle = new Panel
            {
                Location = new Point((392 - 72) / 2, 36),
                Size = new Size(72, 72),
                BackColor = Color.Transparent
            };
            logoCircle.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(UiTheme.Primary))
                    e.Graphics.FillEllipse(brush, 0, 0, 72, 72);
                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillRectangle(brush, 24, 36, 24, 22);
                    using (var pen = new Pen(Color.White, 4))
                        e.Graphics.DrawArc(pen, 26, 18, 20, 22, 180, 180);
                    using (var greenBrush = new SolidBrush(UiTheme.Primary))
                    {
                        e.Graphics.FillEllipse(greenBrush, 32, 42, 8, 7);
                        e.Graphics.FillRectangle(greenBrush, 34, 47, 4, 9);
                    }
                }
            };
            card.Controls.Add(logoCircle);

            _titleLabel = new Label
            {
                Text = "SecureDesktop",
                Font = UiFonts.H1,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(0, 120),
                Size = new Size(392, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            card.Controls.Add(_titleLabel);

            _subtitleLabel = new Label
            {
                Text = "",
                Font = UiFonts.Body,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(0, 152),
                Size = new Size(392, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            card.Controls.Add(_subtitleLabel);

            _idLabel = new Label
            {
                Text = "",
                Font = UiFonts.CaptionBold,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(48, 208),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            card.Controls.Add(_idLabel);

            _idBox = new TextBox
            {
                Location = new Point(48, 232),
                Size = new Size(296, 34),
                Font = UiFonts.BodyLarge,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary
            };
            card.Controls.Add(_idBox);

            _passLabel = new Label
            {
                Text = "",
                Font = UiFonts.CaptionBold,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(48, 284),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            card.Controls.Add(_passLabel);

            _passBox = new TextBox
            {
                Location = new Point(48, 308),
                Size = new Size(296, 34),
                Font = UiFonts.BodyLarge,
                PasswordChar = '●',
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary
            };
            card.Controls.Add(_passBox);

            _loginBtn = new RoundedButton
            {
                Text = "",
                Location = new Point(48, 364),
                Size = new Size(296, 44),
                CornerRadius = 10
            };
            _loginBtn.Click += LoginAction;
            card.Controls.Add(_loginBtn);

            _errorLabel = new Label
            {
                Location = new Point(48, 418),
                Size = new Size(296, 40),
                ForeColor = UiTheme.Danger,
                Font = UiFonts.CaptionBold,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
                BackColor = Color.Transparent
            };
            card.Controls.Add(_errorLabel);

            _hintLabel = new Label
            {
                Text = "",
                Font = UiFonts.Small,
                ForeColor = UiTheme.TextMuted,
                Location = new Point(48, 470),
                Size = new Size(296, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            card.Controls.Add(_hintLabel);

            // 2) DRAG BAR — pasek do przeciągania okna. Wąski pas na górze (0-44 px).
            // Dodawany PO karcie, więc jest NAD kartą (karta i tak zaczyna się od y=80,
            // więc nie ma konfliktu). Ale flagi i X dodane PÓŹNIEJ będą NAD nim.
            var dragBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(480, 44),
                BackColor = Color.Transparent
            };
            dragBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            this.Controls.Add(dragBar);

            // 3) FLAGI językowe (lewy górny róg)
            _langPl = new LanguageButton { LangCode = "pl", Location = new Point(16, 8), Size = new Size(48, 30) };
            _langPl.Click += (s, e) => Loc.SetLanguage("pl");
            this.Controls.Add(_langPl);

            _langGb = new LanguageButton { LangCode = "en", Location = new Point(70, 8), Size = new Size(48, 30) };
            _langGb.Click += (s, e) => Loc.SetLanguage("en");
            this.Controls.Add(_langGb);

            // 4) PRZYCISK X (prawy górny róg) — NAJWIĘKSZY z-order, na samej górze.
            var closeBtn = new CloseButton
            {
                Location = new Point(480 - 44, 6),
                Size = new Size(36, 36)
            };
            closeBtn.Click += (s, e) => Application.Exit();
            this.Controls.Add(closeBtn);

            // Dla pewności — flagi i X na wierzch.
            _langPl.BringToFront();
            _langGb.BringToFront();
            closeBtn.BringToFront();
            dragBar.SendToBack();

            // Enter / klawiatura
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) LoginAction(s, e); };
            _idBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { _passBox.Focus(); e.SuppressKeyPress = true; } };
            _passBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LoginAction(s, e); e.SuppressKeyPress = true; } };

            this.Shown += (s, e) => _idBox.Focus();
        }

        private void LoginAction(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_idBox.Text) || string.IsNullOrEmpty(_passBox.Text))
                {
                    ShowError(Loc.T("login.err.empty"));
                    return;
                }

                var user = _userRepo.GetByIdentificationNumber(_idBox.Text.Trim());
                if (user == null) { ShowError(Loc.T("login.err.invalid")); return; }

                if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.Salt))
                {
                    ShowError(Loc.T("login.err.no_pass"));
                    return;
                }

                var hash = SecurityHelper.HashPassword(_passBox.Text, user.Salt);
                if (!string.Equals(hash, user.PasswordHash, StringComparison.Ordinal))
                {
                    ShowError(Loc.T("login.err.invalid"));
                    return;
                }

                _userRepo.UpdateLastLogin(user.Id);

                var session = new Session
                {
                    UserId = user.Id,
                    IdentificationNumber = user.IdentificationNumber,
                    SessionToken = SecurityHelper.GenerateSessionToken()
                };
                _sessionRepo.Create(session);
                LoggedInSessionId = session.Id;

                _eventRepo.Create(new EventLog
                {
                    UserId = user.Id,
                    IdentificationNumber = user.IdentificationNumber,
                    OperationName = "Login",
                    Result = "Success"
                });

                LoggedInUser = user;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex) { ShowError(Loc.T("login.err.generic") + ex.Message); }
        }

        private void ShowError(string msg)
        {
            _errorLabel.Text = msg;
            _errorLabel.Visible = true;
        }

        internal static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        }
    }
}
