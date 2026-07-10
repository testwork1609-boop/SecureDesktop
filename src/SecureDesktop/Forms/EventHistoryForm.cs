using System;
using System.Drawing;
using System.Windows.Forms;

namespace SecureDesktop.Forms
{
    public class EventHistoryForm : Form
    {
        private ListBox _eventList;

        public EventHistoryForm()
        {
            InitializeComponent();
            LoadEvents();
        }

        private void InitializeComponent()
        {
            this.Text = "Historia zdarzeń";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;

            var title = new Label
            {
                Text = "Historia zdarzeń",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };

            _eventList = new ListBox
            {
                Location = new Point(20, 50),
                Size = new Size(640, 360),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10),
                HorizontalScrollbar = true
            };

            var closeBtn = new Button
            {
                Text = "Zamknij",
                Location = new Point(280, 420),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(0, 120, 212),
                FlatStyle = FlatStyle.Flat
            };
            closeBtn.Click += (s, e) => this.Close();

            var clearBtn = new Button
            {
                Text = "Wyczyść",
                Location = new Point(150, 420),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat
            };
            clearBtn.Click += (s, e) =>
            {
                _eventList.Items.Clear();
                _eventList.Items.Add("[Historia wyczyszczona]");
            };

            Controls.AddRange(new Control[] { title, _eventList, closeBtn, clearBtn });
        }

        private void LoadEvents()
        {
            _eventList.Items.Add("=== HISTORIA ZDARZEŃ ===");
            _eventList.Items.Add("");
            _eventList.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] LOGOWANIE - Sukces - Użytkownik: admin");
            _eventList.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] START - Aplikacja uruchomiona");
            _eventList.Items.Add("");
            _eventList.Items.Add("(Funkcja w rozwoju - dane z pliku JSON)");
        }
    }
}