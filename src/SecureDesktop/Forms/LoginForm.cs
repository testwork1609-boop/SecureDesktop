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
            Color primaryColor = Color.FromArgb(45, 165, 90); // #2DA55A
            Color bgColor = Color.White;
            Color textColor = Color.FromArgb(30, 30, 30);
            Color inputBg = Color.FromArgb(245, 245, 245);

            this.Text = "SecureDesktop - Logowanie";
            this.Size = new Size(420, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.BackColor = bgColor;
            this.ForeColor = textColor;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Panel górny
            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(420, 120),
                BackColor = primaryColor
            };

            var logoLabel = new Label
            {
                Text = "🛡️",
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

            headerPanel.Controls.Add(logoLabel);
            headerPanel.Controls.Add(titleLabel);

            // Formularz logowania
            int y = 150;

            var idLabel = new Label
            {
                Text = "Numer identyfikacyjny",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Location = new Point(50, y),
                Size = new Size(320, 20),
                ForeColor = textColor
            };
            y += 25;

            _idBox = new TextBox
            {
                Location = new Point(50, y),
                Size = new Size(320, 35),
                Font = new Font("Segoe UI", 12),
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            y += 50;

            var passLabel = new Label
            {
                Text = "Hasło",
                Font = new Font("Segoe UI", 10),
                Location = new Point(50, y),
                Size = new Size(320, 20),
                ForeColor = textColor
            };
            y += 25;

            _passBox = new TextBox
            {
                Location = new Point(50, y),
                Size = new Size(320, 35),
                Font = new Font("Segoe UI", 12),
                PasswordChar = '●',
                BackColor = inputBg,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            y += 55;

            // Przycisk logowania
            var loginBtn = new Button
            {
                Text = "Zaloguj się",
                Location = new Point(50, y),
                Size = new Size(320, 42),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            loginBtn.FlatAppearance.BorderSize = 0;
            loginBtn.Click += LoginBtn_Click;
            y += 52;

            // Przycisk zamknij
            var closeBtn = new Button
            {
                Text = "Zamknij",
                Location = new Point(50, y),
                Size = new Size(320, 35),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(100, 100, 100),
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

            // Efekty hover
            loginBtn.MouseEnter += (s, e) => loginBtn.BackColor = Color.FromArgb(40, 180, 100);
            loginBtn.MouseLeave += (s, e) => loginBtn.BackColor = primaryColor;
            closeBtn.MouseEnter += (s, e) => closeBtn.BackColor = Color.FromArgb(245, 245, 245);
            closeBtn.MouseLeave += (s, e) => closeBtn.BackColor = Color.White;

            Controls.AddRange(new Control[] { headerPanel, idLabel, _idBox, passLabel, _passBox, 
                                             loginBtn, closeBtn, _errorLabel });
        }

        private void LoginBtn_Click(object sender, EventArgs e)
        {
            try
            {
                var user = _userRepo.GetByIdentificationNumber(_idBox.Text);
                
                if (user == null)
                {
                    ShowError("Nieprawidłowy login lub hasło");
                    return;
                }

                var hash = Utils.SecurityHelper.HashPassword(_passBox.Text, user.Salt);
                
                if (hash != user.PasswordHash)
                {
                    ShowError("Nieprawidłowy login lub hasło");
                    return;
                }

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

                this.Hide();
                var dashboard = new DashboardForm(user, _db);
                dashboard.FormClosed += (s, args) => this.Close();
                dashboard.Show();
            }
            catch (Exception ex)
            {
                ShowError("Błąd: " + ex.Message);
            }
        }

        private void ShowError(string msg)
        {
            _errorLabel.Text = msg;
            _errorLabel.Visible = true;
        }
    }
}