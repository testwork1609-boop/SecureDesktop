using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;
using SecureDesktop.Services;
using SecureDesktop.Utils;

namespace SecureDesktop.Forms
{
    public class DashboardForm : Form
    {
        private readonly User _currentUser;
        private readonly DatabaseInitializer _db;
        private readonly ScreenLockService _lockService;
        private readonly int _sessionId;

        private int _totalPatterns = 0;
        private int _activePatterns = 0;
        private int _totalEvents = 0;

        private Panel _contentHost;
        private Label _viewTitleLabel;
        private Label _userLabel;
        private bool _lockInProgress;

        public DashboardForm(User user, DatabaseInitializer db, int sessionId)
        {
            _currentUser = user;
            _db = db;
            _sessionId = sessionId;
            _lockService = new ScreenLockService { PasswordVerifier = VerifyCurrentUserPassword };

            _lockService.LockDeactivated += (s, e) =>
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (this.IsDisposed) return;
                        this.WindowState = FormWindowState.Normal;
                        this.Show();
                        this.Activate();
                    }));
                }
                catch { }
            };

            LoadStats();
            InitializeComponent();
            ShowHome();
        }

        private void LoadStats()
        {
            try
            {
                var data = _db.GetData();
                if (data != null)
                {
                    _totalPatterns = data.Patterns == null ? 0 : data.Patterns.Count;
                    _activePatterns = data.Patterns == null ? 0 : data.Patterns.Count(p => p.IsActive);
                    _totalEvents = data.EventLogs == null ? 0 : data.EventLogs.Count;
                }
            }
            catch { }
        }

        private bool VerifyCurrentUserPassword(string password)
        {
            try
            {
                if (string.IsNullOrEmpty(password)) return false;
                var data = _db.GetData();
                if (data == null) return false;

                if (data.Users != null)
                {
                    var user = data.Users.FirstOrDefault(u => u.Id == _currentUser.Id);
                    if (user != null && !string.IsNullOrEmpty(user.PasswordHash) && !string.IsNullOrEmpty(user.Salt))
                    {
                        var hash = SecurityHelper.HashPassword(password, user.Salt);
                        if (string.Equals(hash, user.PasswordHash, StringComparison.Ordinal))
                            return true;
                    }
                }

                if (data.Settings != null)
                {
                    string legacy;
                    if (data.Settings.TryGetValue("AdminPassword", out legacy) &&
                        !string.IsNullOrEmpty(legacy) &&
                        string.Equals(password, legacy, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }
            catch { return false; }
        }

        private PatternRecognitionService CreatePatternRecognitionService()
        {
            double defaultThreshold = 0.75;
            int intervalMs = 200;
            try
            {
                var data = _db.GetData();
                if (data != null && data.Settings != null)
                {
                    string s; int iv;
                    if (data.Settings.TryGetValue("PatternThreshold", out s) &&
                        int.TryParse(s, out iv) && iv >= 1 && iv <= 100)
                        defaultThreshold = iv / 100.0;
                    if (data.Settings.TryGetValue("SearchInterval", out s) &&
                        int.TryParse(s, out iv) && iv > 0)
                        intervalMs = iv;
                }
            }
            catch { }
            return new PatternRecognitionService(defaultThreshold, 0.93, intervalMs);
        }

        private void InitializeComponent()
        {
            this.Icon = Program.AppIcon;
            this.Text = Loc.T("dash.window_title");
            this.Size = new Size(1120, 740);
            this.MinimumSize = new Size(950, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = UiTheme.Bg;
            this.ForeColor = UiTheme.TextPrimary;
            this.DoubleBuffered = true;

            var headerPanel = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = UiTheme.Surface };
            headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(UiTheme.Border, 1))
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var logoMark = new Panel { Location = new Point(24, 20), Size = new Size(32, 32), BackColor = Color.Transparent };
            logoMark.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(UiTheme.Primary)) e.Graphics.FillEllipse(brush, 0, 0, 32, 32);
                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillRectangle(brush, 10, 16, 12, 10);
                    using (var pen = new Pen(Color.White, 2)) e.Graphics.DrawArc(pen, 11, 8, 10, 11, 180, 180);
                }
            };
            headerPanel.Controls.Add(logoMark);

            headerPanel.Controls.Add(new Label
            {
                Text = Loc.T("dash.header_brand"),
                Font = UiFonts.H2,
                Location = new Point(66, 24),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            });

            _viewTitleLabel = new Label
            {
                Text = Loc.T("dash.view_home"),
                Font = UiFonts.Body,
                Location = new Point(220, 28),
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.Transparent
            };
            headerPanel.Controls.Add(_viewTitleLabel);

            _userLabel = new Label
            {
                Text = _currentUser.IdentificationNumber + "  •  " + (_currentUser.IsAdmin ? Loc.T("dash.user_admin") : Loc.T("dash.user_user")),
                Font = UiFonts.Body,
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            headerPanel.Controls.Add(_userLabel);
            headerPanel.Resize += (s, e) => _userLabel.Location = new Point(headerPanel.Width - _userLabel.Width - 32, 28);

            var sidebarPanel = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = UiTheme.Surface };
            sidebarPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(UiTheme.Border, 1))
                    e.Graphics.DrawLine(pen, sidebarPanel.Width - 1, 0, sidebarPanel.Width - 1, sidebarPanel.Height);
            };

            int y = 20;

            var homeBtn = CreateSidebarButton("🏠", Loc.T("dash.sb.home"), y);
            homeBtn.Click += (s, e) => ShowHome();
            y += 48;

            sidebarPanel.Controls.Add(new Panel { Location = new Point(20, y), Size = new Size(210, 1), BackColor = UiTheme.Border });
            y += 16;

            var lockAllBtn = CreateSidebarButton("🔒", Loc.T("dash.sb.lock_all"), y);
            lockAllBtn.Click += OnLockAllScreens;
            y += 48;

            var lockPatternBtn = CreateSidebarButton("🎯", Loc.T("dash.sb.lock_pattern"), y);
            lockPatternBtn.Click += OnLockWithPatterns;
            y += 48;

            var checkpointBtn = CreateSidebarButton("⚡", Loc.T("dash.sb.checkpoint"), y);
            checkpointBtn.Click += OnCheckpoint;
            y += 56;

            sidebarPanel.Controls.Add(homeBtn);
            sidebarPanel.Controls.Add(lockAllBtn);
            sidebarPanel.Controls.Add(lockPatternBtn);
            sidebarPanel.Controls.Add(checkpointBtn);

            if (_currentUser.IsAdmin)
            {
                sidebarPanel.Controls.Add(new Label
                {
                    Text = Loc.T("dash.sb.section_admin"),
                    Font = UiFonts.SmallBold,
                    ForeColor = UiTheme.TextMuted,
                    Location = new Point(24, y),
                    AutoSize = true,
                    BackColor = Color.Transparent
                });
                y += 24;

                var configBtn = CreateSidebarButton("⚙", Loc.T("dash.sb.config"), y);
                configBtn.Click += (s, e) =>
                {
                    var view = new ConfigurationView(_db);
                    view.CloseRequested += () => BeginInvoke(new Action(() => { LoadStats(); ShowHome(); }));
                    view.DataSaved += () => BeginInvoke(new Action(LoadStats));
                    ShowView(view, Loc.T("cfg.title"));
                };
                sidebarPanel.Controls.Add(configBtn);
                y += 48;

                var historyBtn = CreateSidebarButton("📋", Loc.T("dash.sb.history"), y);
                historyBtn.Click += (s, e) =>
                {
                    var view = new EventHistoryView(_db);
                    view.CloseRequested += () => BeginInvoke(new Action(ShowHome));
                    ShowView(view, Loc.T("hist.title"));
                };
                sidebarPanel.Controls.Add(historyBtn);
                y += 48;

                var backupBtn = CreateSidebarButton("💾", Loc.T("dash.sb.backup"), y);
                backupBtn.Click += OnBackupNow;
                sidebarPanel.Controls.Add(backupBtn);
            }

            var logoutHost = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = UiTheme.Surface, Padding = new Padding(16, 10, 16, 10) };
            var logoutBtn = new RoundedButton
            {
                Text = "🚪   " + Loc.T("dash.sb.logout"),
                Dock = DockStyle.Fill,
                CornerRadius = 8,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.DangerSoft,
                PressedColor = UiTheme.DangerSoftHover,
                ForeColor = UiTheme.DangerSoftText,
                Font = UiFonts.BodyBold,
                TextAlign = ContentAlignment.MiddleLeft,
                ButtonPadding = new Padding(14, 0, 8, 0)
            };
            logoutBtn.Click += OnLogout;
            logoutHost.Controls.Add(logoutBtn);
            sidebarPanel.Controls.Add(logoutHost);

            _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Bg, Padding = new Padding(28) };

            this.Controls.Add(_contentHost);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(headerPanel);
        }

        private RoundedButton CreateSidebarButton(string icon, string text, int yPos)
        {
            return new RoundedButton
            {
                Text = icon + "   " + text,
                Location = new Point(16, yPos),
                Size = new Size(218, 44),
                CornerRadius = 8,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.PrimarySoftHover,
                PressedColor = UiTheme.PrimarySoftPressed,
                ForeColor = UiTheme.TextPrimary,
                Font = UiFonts.Body,
                TextAlign = ContentAlignment.MiddleLeft,
                ButtonPadding = new Padding(14, 0, 8, 0)
            };
        }

        private void ShowView(UserControl view, string title)
        {
            _contentHost.SuspendLayout();
            Control old = _contentHost.Controls.Count > 0 ? _contentHost.Controls[0] : null;
            _contentHost.Controls.Clear();
            if (old != null) { try { old.Dispose(); } catch { } }
            view.Dock = DockStyle.Fill;
            _contentHost.Controls.Add(view);
            _contentHost.ResumeLayout();
            _viewTitleLabel.Text = title;
        }

        private void ShowHome()
        {
            LoadStats();
            List<Tip> tips;
            try
            {
                var data = _db.GetData();
                tips = data != null && data.Tips != null
                    ? data.Tips.Where(t => t.IsActive).OrderBy(t => t.Id).ToList()
                    : new List<Tip>();
            }
            catch { tips = new List<Tip>(); }

            var home = new DashboardHomeView(_currentUser, _totalPatterns, _activePatterns, _totalEvents, tips);
            ShowView(home, Loc.T("dash.view_home"));
        }

        private async void OnLockAllScreens(object sender, EventArgs e)
        {
            if (_lockInProgress) return;
            _lockInProgress = true;
            try
            {
                this.WindowState = FormWindowState.Minimized;
                await System.Threading.Tasks.Task.Delay(500);
                _lockService.LockAllScreens();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _lockInProgress = false;
            }
        }

        private async void OnLockWithPatterns(object sender, EventArgs e)
        {
            if (_lockInProgress) return;
            _lockInProgress = true;
            try
            {
                var data = _db.GetData();
                var patterns = data == null || data.Patterns == null
                    ? new List<Pattern>()
                    : data.Patterns.ToList();

                var usable = patterns.Where(p => p.IsActive && p.ImageData != null && p.ImageData.Length > 0).ToList();

                if (usable.Count == 0)
                {
                    MessageBox.Show(Loc.T("dash.msg.no_patterns"), Loc.T("dash.msg.patternlock_title"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                this.WindowState = FormWindowState.Minimized;
                await System.Threading.Tasks.Task.Delay(900);
                _lockService.LockWithPatterns(usable, CreatePatternRecognitionService());
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _lockInProgress = false;
            }
        }

        private void OnCheckpoint(object sender, EventArgs e)
        {
            try
            {
                string path = "notepad.exe";
                string args = "";
                var data = _db.GetData();
                if (data != null && data.Settings != null)
                {
                    string p, a;
                    if (data.Settings.TryGetValue("CheckpointPath", out p) && !string.IsNullOrWhiteSpace(p)) path = p;
                    if (data.Settings.TryGetValue("CheckpointArgs", out a)) args = a == null ? "" : a;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnBackupNow(object sender, EventArgs e)
        {
            try
            {
                var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                if (!File.Exists(sourcePath))
                {
                    MessageBox.Show(Loc.T("dash.msg.no_db"), Loc.T("common.info"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                new BackupService().CreateBackup(sourcePath, "Backup");
                MessageBox.Show(Loc.T("dash.msg.backup_done"), Loc.T("common.success"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("dash.msg.err") + ex.Message, Loc.T("common.error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnLogout(object sender, EventArgs e)
        {
            try
            {
                new EventLogRepository(_db).Create(new EventLog
                {
                    UserId = _currentUser.Id,
                    IdentificationNumber = _currentUser.IdentificationNumber,
                    OperationName = "Logout",
                    Result = "Success"
                });
                new SessionRepository(_db).EndSession(_sessionId);
            }
            catch { }
            this.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_lockService != null) _lockService.UnlockScreens(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    internal class DashboardHomeView : UserControl
    {
        public DashboardHomeView(User user, int totalPatterns, int activePatterns, int totalEvents, List<Tip> tips)
        {
            this.BackColor = UiTheme.Bg;
            this.Dock = DockStyle.Fill;

            var welcomeCard = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(760, 120),
                BackColor = UiTheme.Bg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            UiTheme.MakeCard(welcomeCard, 12);

            welcomeCard.Controls.Add(new Label
            {
                Text = Loc.T("dash.home.greeting", user.IdentificationNumber),
                Font = UiFonts.H1,
                Location = new Point(28, 24),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            });
            welcomeCard.Controls.Add(new Label
            {
                Text = Loc.T("dash.home.logged_at", DateTime.Now.ToString("dddd, d MMMM yyyy — HH:mm")),
                Font = UiFonts.Body,
                Location = new Point(28, 68),
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent
            });
            this.Controls.Add(welcomeCard);

            int tipCardTop = 144;

            if (user.IsAdmin)
            {
                var statsRow = new Panel
                {
                    Location = new Point(0, 144),
                    Size = new Size(760, 130),
                    BackColor = UiTheme.Bg,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var c1 = CreateStatCard("🎯", totalPatterns.ToString(), Loc.T("dash.home.stat_patterns"), Loc.T("dash.home.stat_patterns_sub", activePatterns));
                var c2 = CreateStatCard("📋", totalEvents.ToString(), Loc.T("dash.home.stat_events"), Loc.T("dash.home.stat_events_sub"));
                var c3 = CreateStatCard("✅", "OK", Loc.T("dash.home.stat_status"), Loc.T("dash.home.stat_status_sub"));

                c1.Location = new Point(0, 0); c2.Location = new Point(264, 0); c3.Location = new Point(528, 0);
                c1.Size = c2.Size = c3.Size = new Size(248, 130);

                statsRow.Controls.Add(c1); statsRow.Controls.Add(c2); statsRow.Controls.Add(c3);
                this.Controls.Add(statsRow);
                tipCardTop = 296;
            }

            var tipCard = new Panel
            {
                Location = new Point(0, tipCardTop),
                Size = new Size(760, 320),
                BackColor = UiTheme.Bg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            UiTheme.MakeCard(tipCard, 12);

            tipCard.Controls.Add(new Label
            {
                Text = "💡",
                Font = new Font("Segoe UI", 18),
                Location = new Point(20, 14),
                Size = new Size(40, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });

            tipCard.Controls.Add(new Label
            {
                Text = Loc.T("dash.home.tips"),
                Font = UiFonts.H3,
                Location = new Point(68, 20),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            });

            var scroll = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(tipCard.Width - 40, tipCard.Height - 80),
                BackColor = Color.Transparent,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            tipCard.Controls.Add(scroll);
            PopulateTips(scroll, tips, tipCard.Width - 60);

            tipCard.Resize += (s, e) =>
            {
                scroll.Size = new Size(tipCard.Width - 40, tipCard.Height - 80);
                PopulateTips(scroll, tips, tipCard.Width - 60);
            };

            this.Controls.Add(tipCard);
        }

        private void PopulateTips(Panel container, List<Tip> tips, int width)
        {
            container.SuspendLayout();
            var toRemove = new List<Control>();
            foreach (Control c in container.Controls) toRemove.Add(c);
            foreach (var c in toRemove) { container.Controls.Remove(c); c.Dispose(); }

            if (width < 100) width = 100;

            if (tips == null || tips.Count == 0)
            {
                container.Controls.Add(new Label
                {
                    Text = Loc.T("dash.home.tips_empty"),
                    Font = UiFonts.Body,
                    ForeColor = UiTheme.TextMuted,
                    Location = new Point(0, 0),
                    AutoSize = true,
                    MaximumSize = new Size(width, 0),
                    BackColor = Color.Transparent
                });
                container.ResumeLayout();
                return;
            }

            int y = 0;
            for (int i = 0; i < tips.Count; i++)
            {
                var tip = tips[i];

                var titleLbl = new Label
                {
                    Text = (tip.Title ?? "").ToUpperInvariant(),
                    Font = UiFonts.CaptionBold,
                    ForeColor = UiTheme.PrimarySoftText,
                    Location = new Point(0, y),
                    AutoSize = true,
                    MaximumSize = new Size(width, 0),
                    BackColor = Color.Transparent
                };
                container.Controls.Add(titleLbl);
                y += titleLbl.PreferredHeight + 4;

                var contentLbl = new Label
                {
                    Text = tip.Content ?? "",
                    Font = UiFonts.Body,
                    ForeColor = UiTheme.TextSecondary,
                    Location = new Point(0, y),
                    AutoSize = true,
                    MaximumSize = new Size(width, 0),
                    BackColor = Color.Transparent
                };
                container.Controls.Add(contentLbl);
                y += contentLbl.PreferredHeight + 16;

                if (i < tips.Count - 1)
                {
                    container.Controls.Add(new Panel
                    {
                        Location = new Point(0, y - 8),
                        Size = new Size(width, 1),
                        BackColor = UiTheme.Border
                    });
                }
            }
            container.ResumeLayout();
        }

        private Panel CreateStatCard(string icon, string value, string label, string subtext)
        {
            var card = new Panel { BackColor = UiTheme.Bg };
            UiTheme.MakeCard(card, 12);

            card.Controls.Add(new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 18),
                Location = new Point(20, 18),
                Size = new Size(40, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            card.Controls.Add(new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                Location = new Point(70, 20),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent
            });
            card.Controls.Add(new Label
            {
                Text = label,
                Font = UiFonts.Caption,
                Location = new Point(20, 74),
                AutoSize = true,
                ForeColor = UiTheme.TextSecondary,
                BackColor = Color.Transparent
            });
            card.Controls.Add(new Label
            {
                Text = subtext,
                Font = UiFonts.Small,
                Location = new Point(20, 94),
                AutoSize = true,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.Transparent
            });

            return card;
        }
    }
}
