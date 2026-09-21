using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Database.Repositories;
using SecureDesktop.Models;
using SecureDesktop.Utils;

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
            _eventRepo = new EventLogRepository(_db);
            InitializeComponent();
            LoadEvents();
        }

        private void InitializeComponent()
        {
            Color primaryColor = Color.FromArgb(45, 165, 90);

            this.Text = "Historia zdarzeń";
            this.Size = new Size(820, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.ForeColor = Color.FromArgb(30, 30, 30);
            this.Icon = Program.AppIcon;
            this.KeyPreview = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) this.Close();
            };

            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(820, 50),
                BackColor = primaryColor
            };

            var title = new Label
            {
                Text = "📊  Historia zdarzeń",
                Font = UiFonts.Segoe14Bold,
                Location = new Point(20, 12),
                AutoSize = true,
                ForeColor = Color.White
            };

            headerPanel.Controls.Add(title);

            _eventList = new ListBox
            {
                Location = new Point(15, 65),
                Size = new Size(775, 400),
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(30, 30, 30),
                Font = UiFonts.Consolas10,
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 20
            };

            _eventList.DrawItem += EventList_DrawItem;

            var refreshBtn = new Button
            {
                Text = "Odśwież",
                Location = new Point(15, 480),
                Size = new Size(100, 35),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = UiFonts.Segoe10,
                Cursor = Cursors.Hand
            };
            refreshBtn.FlatAppearance.BorderSize = 0;
            refreshBtn.Click += (s, e) => LoadEvents();

            var clearBtn = new Button
            {
                Text = "Wyczyść wszystko",
                Location = new Point(125, 480),
                Size = new Size(140, 35),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(220, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Font = UiFonts.Segoe10,
                Cursor = Cursors.Hand
            };
            clearBtn.FlatAppearance.BorderColor = Color.FromArgb(220, 80, 80);
            clearBtn.FlatAppearance.BorderSize = 1;
            clearBtn.Click += (s, e) =>
            {
                var result = MessageBox.Show(
                    "Czy na pewno usunąć CAŁĄ historię zdarzeń z bazy?",
                    "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;

                try
                {
                    var data = _db.GetData();
                    if (data?.EventLogs != null)
                    {
                        data.EventLogs.Clear();
                        data.NextEventId = 1;
                        _db.Save();
                    }
                    LoadEvents();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd czyszczenia: " + ex.Message, "Błąd");
                }
            };

            var closeBtn = new Button
            {
                Text = "Zamknij",
                Location = new Point(690, 480),
                Size = new Size(100, 35),
                BackColor = Color.White,
                ForeColor = primaryColor,
                FlatStyle = FlatStyle.Flat,
                Font = UiFonts.Segoe10,
                Cursor = Cursors.Hand
            };
            closeBtn.FlatAppearance.BorderColor = primaryColor;
            closeBtn.FlatAppearance.BorderSize = 1;
            closeBtn.Click += (s, e) => this.Close();

            Controls.AddRange(new Control[] { headerPanel, _eventList, refreshBtn, clearBtn, closeBtn });
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
                {
                    string line = FormatEvent(e);
                    _eventList.Items.Add(line);
                }
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

            return $"[{time}] {icon} {op,-15} | {result,-10} | {user}{desc}";
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
    }
}
