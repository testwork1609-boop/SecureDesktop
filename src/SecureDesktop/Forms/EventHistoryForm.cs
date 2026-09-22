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
            this.BackColor = UiTheme.Bg;
            this.Dock = DockStyle.Fill;
            InitializeComponent();
            LoadEvents();
        }

        private void InitializeComponent()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = UiTheme.Bg
            };

            var title = new Label
            {
                Text = "Historia zdarzeń",
                Font = UiFonts.H1,
                Location = new Point(0, 8),
                AutoSize = true,
                ForeColor = UiTheme.TextPrimary
            };
            var subtitle = new Label
            {
                Text = "Wszystkie operacje zarejestrowane w bazie",
                Font = UiFonts.Body,
                Location = new Point(0, 34),
                AutoSize = true,
                ForeColor = UiTheme.TextMuted
            };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = UiTheme.Bg,
                Padding = new Padding(0, 12, 0, 12)
            };

            var refreshBtn = new RoundedButton
            {
                Text = "🔄  Odśwież",
                Location = new Point(0, 12),
                Size = new Size(140, 42),
                CornerRadius = 10
            };
            refreshBtn.Click += (s, e) => LoadEvents();

            var clearBtn = new RoundedButton
            {
                Text = "🗑  Wyczyść wszystko",
                Location = new Point(152, 12),
                Size = new Size(200, 42),
                CornerRadius = 10,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.DangerLight,
                PressedColor = UiTheme.DangerLight,
                ForeColor = UiTheme.Danger,
                OutlineColor = UiTheme.Danger,
                OutlineThickness = 1
            };
            clearBtn.Click += OnClearAll;

            var backBtn = new RoundedButton
            {
                Text = "Powrót do panelu",
                Location = new Point(364, 12),
                Size = new Size(180, 42),
                CornerRadius = 10,
                NormalColor = UiTheme.Surface,
                HoverColor = UiTheme.SurfaceAlt,
                PressedColor = UiTheme.SurfaceAlt,
                ForeColor = UiTheme.TextSecondary,
                OutlineColor = UiTheme.BorderStrong,
                OutlineThickness = 1
            };
            backBtn.Click += (s, e) => { var h = CloseRequested; if (h != null) h(); };

            bottomPanel.Controls.Add(refreshBtn);
            bottomPanel.Controls.Add(clearBtn);
            bottomPanel.Controls.Add(backBtn);

            _eventList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                Font = UiFonts.MonoSmall,
                BorderStyle = BorderStyle.None,
                HorizontalScrollbar = true,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24,
                IntegralHeight = false
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
            string icon = "•";
            if (string.Equals(e.Result, "Success", StringComparison.OrdinalIgnoreCase)) icon = "✅";
            else if (string.Equals(e.Result, "Warning", StringComparison.OrdinalIgnoreCase)) icon = "⚠";
            else if (string.Equals(e.Result, "Error", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(e.Result, "Failure", StringComparison.OrdinalIgnoreCase)) icon = "✕";

            string user = string.IsNullOrEmpty(e.IdentificationNumber) ? "—" : e.IdentificationNumber;
            string op = e.OperationName ?? "—";
            string result = e.Result ?? "—";
            string desc = string.IsNullOrWhiteSpace(e.Description) ? "" : "  |  " + e.Description;

            return "[" + time + "]  " + icon + "  " + op + "  |  " + result + "  |  " + user + desc;
        }

        private void EventList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            string text = _eventList.Items[e.Index] as string ?? "";
            Color fore = UiTheme.TextSecondary;

            if (text.Contains("✅")) fore = UiTheme.Primary;
            else if (text.Contains("⚠")) fore = UiTheme.Warning;
            else if (text.Contains("✕")) fore = UiTheme.Danger;
            else if (text.StartsWith("═══")) fore = UiTheme.TextMuted;

            using (var brush = new SolidBrush(fore))
                e.Graphics.DrawString(text, e.Font, brush, e.Bounds.X + 8, e.Bounds.Y + 4);

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
