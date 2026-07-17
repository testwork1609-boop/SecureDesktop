using System;
using System.Drawing;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;

namespace SecureDesktop.Forms
{
    public class EventHistoryForm : Form
    {
        private readonly DatabaseInitializer _db;
        private readonly EventLogRepository _eventRepo;
        private ListBox _eventList;

        public EventHistoryForm(DatabaseInitializer db)
        {
            _db = db;
            _eventRepo = new EventLogRepository(db);

            Color primaryColor = Color.FromArgb(45, 165, 90);

            this.Text = "Historia zdarzeń";
            this.Size = new Size(750, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.ForeColor = Color.FromArgb(30, 30, 30);
            this.Icon = Program.AppIcon;
            this.KeyPreview = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // ESC = zamknij
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    this.Close();
            };

            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(750, 50),
                BackColor = primaryColor
            };

            var title = new Label
            {
                Text = "📊  Historia zdarzeń",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(20, 12),
                AutoSize = true,
                ForeColor = Color.White
            };

            headerPanel.Controls.Add(title);

            _eventList = new ListBox
            {
                Location = new Point(15, 65),
                Size = new Size(705, 340),
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(30, 30, 30),
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true,
                DrawMode = DrawMode.OwnerFixedHeight,
                ItemHeight = 20
            };
            _eventList.DrawItem += EventList_DrawItem;

            var refreshBtn = new Button
            {
                Text = "Odśwież",
                Location = new Point(130, 420),
                Size = new Size(100, 35),
                BackColor = Color.White,
                ForeColor = primaryColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            refreshBtn.FlatAppearance.BorderColor = primaryColor;
            refreshBtn.FlatAppearance.BorderSize = 1;
            refreshBtn.Click += (s, e) => LoadEvents();

            var clearBtn = new Button
            {
                Text = "Wyczyść widok",
                Location = new Point(250, 420),
                Size = new Size(120, 35),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(220, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            clearBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 80, 80);
            clearBtn.FlatAppearance.BorderSize = 1;
            clearBtn.Click += (s, e) =>
            {
                // Czysci tylko widok - nie usuwa zapisanej historii z bazy,
                // zeby nie stracic zapisu audytowego.
                _eventList.Items.Clear();
                _eventList.Items.Add("[Widok wyczyszczony - kliknij \"Odśwież\" aby zaladowac ponownie]");
            };

            var closeBtn = new Button
            {
                Text = "Zamknij",
                Location = new Point(390, 420),
                Size = new Size(100, 35),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => this.Close();

            LoadEvents();

            Controls.AddRange(new Control[] { headerPanel, _eventList, refreshBtn, clearBtn, closeBtn });
        }

        private void LoadEvents()
        {
            _eventList.Items.Clear();

            var events = _eventRepo.GetAll();

            if (events.Count == 0)
            {
                _eventList.Items.Add("(Brak zarejestrowanych zdarzeń)");
                return;
            }

            foreach (var ev in events)
            {
                _eventList.Items.Add(ev);
            }
        }

        private void EventList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            string text;
            Color color = Color.FromArgb(30, 30, 30);

            if (_eventList.Items[e.Index] is EventLog ev)
            {
                string icon = IconFor(ev);
                string who = string.IsNullOrEmpty(ev.IdentificationNumber) ? "" : $" - {ev.IdentificationNumber}";
                string desc = string.IsNullOrEmpty(ev.Description) ? "" : $" | {ev.Description}";

                text = $"[{ev.Timestamp:yyyy-MM-dd HH:mm:ss}] {icon} {ev.OperationName}{who} - {ev.Result}{desc}";

                color = SeverityColor(ev);
            }
            else
            {
                text = _eventList.Items[e.Index].ToString();
            }

            using (var brush = new SolidBrush(color))
            {
                e.Graphics.DrawString(text, e.Font ?? _eventList.Font, brush, e.Bounds);
            }

            e.DrawFocusRectangle();
        }

        private string IconFor(EventLog ev)
        {
            switch (ev.OperationName)
            {
                case "Login": return "✅";
                case "Logout": return "🚪";
                case "Backup": return "💾";
                case "FileModified": return "⚠️";
                default: return "•";
            }
        }

        private Color SeverityColor(EventLog ev)
        {
            if (string.Equals(ev.Severity, "Warning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ev.Severity, "Error", StringComparison.OrdinalIgnoreCase) ||
                ev.OperationName == "FileModified")
            {
                return Color.FromArgb(200, 120, 0);
            }

            if (string.Equals(ev.Result, "Success", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(30, 30, 30);

            return Color.FromArgb(150, 150, 150);
        }
    }
}