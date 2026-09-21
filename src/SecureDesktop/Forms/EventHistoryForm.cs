using System;
using System.Drawing;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;
using SecureDesktop.Utils;

namespace SecureDesktop.Forms
{
    public class EventHistoryView : UserControl
    {
        private readonly DatabaseInitializer _db;
        private readonly EventLogRepository _eventRepo;
        private ListBox _eventList;

        public event Action CloseRequested;

        public EventHistoryView(DatabaseInitializer db)
        {
            _db = db;
            _eventRepo = new EventLogRepository(_db);
            this.BackColor = Color.White;
            this.Dock = DockStyle.Fill;
            InitializeComponent();
            LoadEvents();
        }

        private void InitializeComponent()
        {
            Color primaryColor = Color.FromArgb(45, 165, 90);

            var header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(248, 249, 250) };

            var title = new Label
            {
                Text = "📊  Historia zdarzeń",
                Font = UiFonts.Segoe14Bold,
                Location = new Point(16, 12),
                AutoSize = true,
                ForeColor = primaryColor
            };
            header.Controls.Add(title);

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(245, 245, 245) };

            var refreshBtn = new Button
            {
                Text = "🔄  Odśwież", Location = new Point(16, 12), Size = new Size(110, 36),
                BackColor = primaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Font = UiFonts.Segoe10, Cursor = Cursors.Hand
            };
            refreshBtn.FlatAppearance.BorderSize = 0;
            refreshBtn.Click += (s, e) => LoadEvents();

            var clearBtn = new Button
            {
                Text = "🗑  Wyczyść wszystko", Location = new Point(136, 12), Size = new Size(160, 36),
                BackColor = Color.White, ForeColor = Color.FromArgb(220, 80, 80),
                FlatStyle = FlatStyle.Flat, Font = UiFonts.Segoe10, Cursor = Cursors.Hand
            };
            clearBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 80, 80);
            clearBtn.FlatAppearance.BorderSize = 1;
            clearBtn.Click += OnClearAll;

            var backBtn = new Button
            {
                Text = "Powrót do panelu", Location = new Point(306, 12), Size = new Size(160, 36),
                BackColor = Color.White, ForeColor = Color.FromArgb(100, 100, 100),
                FlatStyle = FlatStyle.Flat, Font = UiFonts.Segoe10, Cursor = Cursors.Hand
            };
            backBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 220, 220);
            backBtn.FlatAppearance.BorderSize = 1;
            backBtn.Click += (s, e) => { var h = CloseRequested; if (h != null) h(); };

            bottomPanel.Controls.Add(refreshBtn);
            bottomPanel.Controls.Add(clearBtn);
            bottomPanel.Controls.Add(backBtn);

            _eventList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(30, 30, 30),
                Font = UiFonts.Consolas10,
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 20
            };
            _eventList.DrawItem += EventList_DrawItem;

            this.Controls.Add(_eventList);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(header);
        }

        private void LoadEvents()
        {
            try
            {
                _eventList.Items.Clear();
                var events = _eventRepo.GetAll();
                if (events == null || events.Count == 0)
                {
                    _eventList.Items.Add("");
                    _eventList.Items.Add("  Brak zapisanych zdarzeń.");
                    return;
                }

                _eventList.Items.Add("═══ HISTORIA ZDARZEŃ  (" + events.Count + " wpisów) ═══");
                _eventList.Items.Add("");
                foreach (var e in events)
                    _eventList.Items.Add(FormatEvent(e));
            }
            catch (Exception ex)
            {
                _eventList.Items.Clear();
                _eventList.Items.Add("Błąd wczytywania historii: " + ex.Message);
            }
        }

        private static string FormatEvent(EventLog e)
        {
            string time = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            string icon = "ℹ️";
            if (string.Equals(e.Result, "Success", StringComparison.OrdinalIgnoreCase)) icon = "✅";
            else if (string.Equals(e.Result, "Warning", StringComparison.OrdinalIgnoreCase)) icon = "⚠️";
            else if (string.Equals(e.Result, "Error", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(e.Result, "Failure", StringComparison.OrdinalIgnoreCase)) icon = "❌";

            string user = string.IsNullOrEmpty(e.IdentificationNumber) ? "—" : e.IdentificationNumber;
            string op = e.OperationName ?? "—";
            string result = e.Result ?? "—";
            string desc = string.IsNullOrWhiteSpace(e.Description) ? "" : "  |  " + e.Description;

            return "[" + time + "] " + icon + " " + op + " | " + result + " | " + user + desc;
        }

        private void EventList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            string text = _eventList.Items[e.Index] as string ?? "";
            Color fore = e.ForeColor;

            if (text.Contains("✅")) fore = Color.FromArgb(35, 130, 70);
            else if (text.Contains("⚠️")) fore = Color.FromArgb(200, 130, 0);
            else if (text.Contains("❌")) fore = Color.FromArgb(190, 50, 50);
            else if (text.StartsWith("═══")) fore = Color.FromArgb(45, 165, 90);

            using (var brush = new SolidBrush(fore))
                e.Graphics.DrawString(text, e.Font, brush, e.Bounds);

            e.DrawFocusRectangle();
        }

        private void OnClearAll(object sender, EventArgs e)
        {
            if (MessageBox.Show("Czy na pewno usunąć CAŁĄ historię zdarzeń z bazy?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                var data = _db.GetData();
                if (data != null && data.EventLogs != null)
                {
                    data.EventLogs.Clear();
                    data.NextEventId = 1;
                    _db.Save();
                }
                LoadEvents();
            }
            catch (Exception ex) { MessageBox.Show("Błąd czyszczenia: " + ex.Message, "Błąd"); }
        }
    }
}
