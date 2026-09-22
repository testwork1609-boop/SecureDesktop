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

        public User LoggedInUser { get; private set; }
        public int LoggedInSessionId { get; private set; }

        public LoginForm(DatabaseInitializer db)
        {
            _db = db;
            _userRepo = new UserRepository(_db);
            _sessionRepo = new SessionRepository(_db);
            _eventRepo = new EventLogRepository(_db);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "SecureDesktop — Logowanie";
            this.Size = new Size(480, 640);
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

            // Drag bar (przeciąganie okna za górną krawędź)
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

            // === Close button (X) - własnoręcznie rysowany ===
            var closeBtn = new CloseButton
            {
                Location = new Point(480 - 44, 8),
                Size = new Size(36, 36),
                BackColor = Color.Transparent
            };
            closeBtn.Click += (s, e) => Application.Exit();
            this.Controls.Add(closeBtn);
            closeBtn.BringToFront();

            // === Card ===
            var card = new Panel
            {
                Location = new Point(44, 80),
                Size = new Size(392, 500),
                BackColor = UiTheme.Bg
            };
            UiTheme.MakeCard(card, 14, true, true);

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

            card.Controls.Add(new Label
            {
                Text = "SecureDesktop",
                Font = UiFonts.H1,
                ForeColor = UiTheme.TextPrimary,
                Location = new Point(0, 120),
                Size = new Size(392, 32),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });

            card.Controls.Add(new Label
            {
                Text = "Zaloguj się, aby kontynuować",
                Font = UiFonts.Body,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(0, 152),
                Size = new Size(392, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });

            card.Controls.Add(new Label
            {
                Text = "Numer identyfikacyjny",
                Font = UiFonts.CaptionBold,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(48, 202),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            _idBox = new TextBox
            {
                Location = new Point(48, 226),
                Size = new Size(296, 34),
                Font = UiFonts.BodyLarge,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary
            };
            card.Controls.Add(_idBox);

            card.Controls.Add(new Label
            {
                Text = "Hasło",
                Font = UiFonts.CaptionBold,
                ForeColor = UiTheme.TextSecondary,
                Location = new Point(48, 278),
                AutoSize = true,
                BackColor = Color.Transparent
            });

            _passBox = new TextBox
            {
                Location = new Point(48, 302),
                Size = new Size(296, 34),
                Font = UiFonts.BodyLarge,
                PasswordChar = '●',
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary
            };
            card.Controls.Add(_passBox);

            var loginBtn = new RoundedButton
            {
                Text = "Zaloguj się",
                Location = new Point(48, 358),
                Size = new Size(296, 44),
                CornerRadius = 10
            };
            loginBtn.Click += LoginAction;
            card.Controls.Add(loginBtn);

            _errorLabel = new Label
            {
                Location = new Point(48, 410),
                Size = new Size(296, 40),
                ForeColor = UiTheme.Danger,
                Font = UiFonts.CaptionBold,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
                BackColor = Color.Transparent
            };
            card.Controls.Add(_errorLabel);

            card.Controls.Add(new Label
            {
                Text = "Domyślnie: admin / admin",
                Font = UiFonts.Small,
                ForeColor = UiTheme.TextMuted,
                Location = new Point(48, 456),
                Size = new Size(296, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });

            this.Controls.Add(card);

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
                    ShowError("Podaj numer identyfikacyjny i hasło");
                    return;
                }

                var user = _userRepo.GetByIdentificationNumber(_idBox.Text.Trim());
                if (user == null) { ShowError("Nieprawidłowy login lub hasło"); return; }

                if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.Salt))
                {
                    ShowError("Konto nie ma ustawionego hasła");
                    return;
                }

                var hash = SecurityHelper.HashPassword(_passBox.Text, user.Salt);
                if (!string.Equals(hash, user.PasswordHash, StringComparison.Ordinal))
                {
                    ShowError("Nieprawidłowy login lub hasło");
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
            catch (Exception ex) { ShowError("Błąd: " + ex.Message); }
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
