using System;
using System.Drawing;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;

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
            Color primaryColor = Color.FromArgb(45, 165, 90);

            this.Text = "SecureDesktop - Logowanie";
            this.Size = new Size(420, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.BackColor = Color.White;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Icon = Program.AppIcon;
            this.KeyPreview = true;

            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(420, 120),
                BackColor = primaryColor
            };

            var iconLabel = new Label
            {
                Text = "🔒",
                Font = new Font("Segoe UI", 36),
                Location = new Point(175, 10),
                Size = new Size(70, 50),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White
            };

            var titleLabel = new Label
            {
                Text = "SecureDesktop",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                Location = new Point(80, 60),
                Size = new Size(260, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White
            };

            headerPanel.Controls.Add(iconLabel);
            headerPanel.Controls.Add(titleLabel);

            int y = 150;
            var idLabel = new Label { Text = "Numer identyfikacyjny", Location = new Point(50, y), Size = new Size(320, 20), Font = new Font("Segoe UI", 10) };
            y += 25;
            _idBox = new TextBox { Location = new Point(50, y), Size = new Size(320, 35), Font = new Font("Segoe UI", 12), BackColor = Color.FromArgb(245, 245, 245), BorderStyle = BorderStyle.FixedSingle };
            y += 50;
            var passLabel = new Label { Text = "Haslo", Location = new Point(50, y), Size = new Size(320, 20), Font = new Font("Segoe UI", 10) };
            y += 25;
            _passBox = new TextBox { Location = new Point(50, y), Size = new Size(320, 35), Font = new Font("Segoe UI", 12), PasswordChar = '●', BackColor = Color.FromArgb(245, 245, 245), BorderStyle = BorderStyle.FixedSingle };
            y += 55;

            var loginBtn = new Button
            {
                Text = "Zaloguj sie",
                Location = new Point(50, y),
                Size = new Size(320, 42),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            loginBtn.FlatAppearance.BorderSize = 0;
            loginBtn.Click += LoginAction;
            y += 52;

            var closeBtn = new Button
            {
                Text = "Zamknij",
                Location = new Point(50, y),
                Size = new Size(320, 35),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            closeBtn.FlatAppearance.BorderSize = 1;
            closeBtn.Click += (s, e) => Application.Exit();

            _errorLabel = new Label
            {
                Location = new Point(50, y + 45),
                Size = new Size(320, 25),
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                Visible = false
            };

            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) LoginAction(s, e); };
            _idBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { _passBox.Focus(); e.SuppressKeyPress = true; } };
            _passBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { LoginAction(s, e); e.SuppressKeyPress = true; } };

            Controls.AddRange(new Control[] { headerPanel, idLabel, _idBox, passLabel, _passBox, loginBtn, closeBtn, _errorLabel });
        }

        private void LoginAction(object sender, EventArgs e)
        {
            try
            {
                // Pobranie wspólnego hasła z ustawień
                var adminPassword = "admin"; // domyślne, jeśli brak w ustawieniach
                var settings = _db.GetData()?.Settings;
                if (settings != null && settings.ContainsKey("AdminPassword"))
                {
                    adminPassword = settings["AdminPassword"];
                }

                // Weryfikacja hasła
                if (_passBox.Text != adminPassword)
                {
                    ShowError("Nieprawidlowy login lub haslo");
                    return;
                }

                // Wyszukanie użytkownika po numerze ID
                var user = _userRepo.GetByIdentificationNumber(_idBox.Text);
                if (user == null)
                {
                    ShowError("Uzytkownik nie istnieje");
                    return;
                }

                // Zalogowano pomyślnie
                _userRepo.UpdateLastLogin(user.Id);

                var session = new Session
                {
                    UserId = user.Id,
                    IdentificationNumber = user.IdentificationNumber,
                    SessionToken = Utils.SecurityHelper.GenerateSessionToken()
                };
                _sessionRepo.Create(session);

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
            catch (Exception ex)
            {
                ShowError("Blad: " + ex.Message);
            }
        }

        private void ShowError(string msg)
        {
            _errorLabel.Text = msg;
            _errorLabel.Visible = true;
        }
    }
}