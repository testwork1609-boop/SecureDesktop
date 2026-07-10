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
            Color primaryColor = Color.FromArgb(45, 165, 90);
            
            this.Text = "Historia zdarzeń";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.ForeColor = Color.FromArgb(30, 30, 30);

            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(700, 50),
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
                Size = new Size(655, 340),
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(30, 30, 30),
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.FixedSingle,
                HorizontalScrollbar = true
            };

            var clearBtn = new Button
            {
                Text = "Wyczyść",
                Location = new Point(180, 420),
                Size = new Size(100, 35),
                BackColor = Color.White,
                ForeColor = primaryColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            clearBtn.FlatAppearance.BorderColor = primaryColor;
            clearBtn.FlatAppearance.BorderSize = 1;
            clearBtn.Click += (s, e) => { _eventList.Items.Clear(); _eventList.Items.Add("[Historia wyczyszczona]"); };

            var closeBtn = new Button
            {
                Text = "Zamknij",
                Location = new Point(300, 420),
                Size = new Size(100, 35),
                BackColor = primaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => this.Close();

            LoadEvents();

            Controls.AddRange(new Control[] { headerPanel, _eventList, clearBtn, closeBtn });
        }

        private void LoadEvents()
        {
            _eventList.Items.Add("═══ HISTORIA ZDARZEŃ ═══");
            _eventList.Items.Add("");
            _eventList.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ✅ LOGOWANIE - Sukces - admin");
            _eventList.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 🚀 START - Aplikacja uruchomiona");
            _eventList.Items.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 💾 BACKUP - Wykonano kopię zapasową");
            _eventList.Items.Add("");
            _eventList.Items.Add("(Funkcja w rozwoju - pełna historia już wkrótce)");
        }
    }
}