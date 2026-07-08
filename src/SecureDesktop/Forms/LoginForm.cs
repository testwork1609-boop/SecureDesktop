using System;
using System.Drawing;
using System.Windows.Forms;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;
using SecureDesktop.Services;

namespace SecureDesktop.Forms
{
    public class LoginForm : Form
    {
        private readonly UserRepository _userRepo;
        private readonly SessionRepository _sessionRepo;
        private readonly EventLogRepository _eventRepo;
        private TextBox _idBox;
        private TextBox _passBox;
        private Label _errorLabel;

        public LoginForm(string connectionString)
        {
            _userRepo = new UserRepository(connectionString);
            _sessionRepo = new SessionRepository(connectionString);
            _eventRepo = new EventLogRepository(connectionString);
            
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "SecureDesktop - Logowanie";
            this.Size = new Size(400, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;

            var titleLabel = new Label
            {
                Text = "SecureDesktop",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                Location = new Point(100, 40),
                Size = new Size(200, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var idLabel = new Label
            {
                Text = "Numer identyfikacyjny:",
                Location = new Point(50, 150),
                Size = new Size(300, 20)
            };

            _idBox = new TextBox
            {
                Location = new Point(50, 175),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 12)
            };

            var passLabel = new Label
            {
                Text = "Hasło:",
                Location = new Point(50, 230),
                Size = new Size(300, 20)
            };

            _passBox = new TextBox
            {
                Location = new Point(50, 255),
                Size = new Size(300, 30),
                Font = new Font("Segoe UI", 12),
                PasswordChar = '●'
            };

            var loginBtn = new Button
            {
                Text = "Zaloguj",
                Location = new Point(50, 320),
                Size = new Size(300, 40),
                BackColor = Color.FromArgb(0, 120, 212),
                FlatStyle = FlatStyle.Flat
            };
            loginBtn.Click += LoginBtn_Click;

            var closeBtn = new Button
            {
                Text = "Zamknij",
                Location = new Point(50, 375),
                Size = new Size(300, 35),
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat
            };
            closeBtn.Click += (s, e) => Application.Exit();

            _errorLabel = new Label
            {
                Location = new Point(50, 425),
                Size = new Size(300, 30),
                ForeColor = Color.Red,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            Controls.AddRange(new Control[] { titleLabel, idLabel, _idBox, passLabel, _passBox, 
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

                // Successful login
                _userRepo.UpdateLastLogin(user.Id);
                
                var session = new Session
                {
                    UserId = user.Id,
                    IdentificationNumber = user.IdentificationNumber,
                    LoginTime = DateTime.Now,
                    SessionToken = Utils.SecurityHelper.GenerateSessionToken()
                };
                _sessionRepo.Create(session);

                _eventRepo.Create(new EventLog
                {
                    UserId = user.Id,
                    IdentificationNumber = user.IdentificationNumber,
                    OperationName = "Login",
                    Result = "Success",
                    Timestamp = DateTime.Now
                });

                this.Hide();
                var dashboard = new DashboardForm(user);
                dashboard.FormClosed += (s, args) => this.Close();
                dashboard.Show();
            }
            catch (Exception ex)
            {
                ShowError("Błąd logowania: " + ex.Message);
            }
        }

        private void ShowError(string msg)
        {
            _errorLabel.Text = msg;
            _errorLabel.Visible = true;
        }
    }
}