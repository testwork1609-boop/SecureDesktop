using System;
using System.IO;
using System.Windows.Forms;

namespace SecureDesktop
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Pętla logowania - po wylogowaniu wróć do logowania
            while (true)
            {
                // Utwórz foldery
                foreach (var dir in new[] { "Database", "Backup", "Logs" })
                {
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }

                // Inicjalizacja bazy
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                var db = new Database.DatabaseInitializer(dbPath);
                db.Initialize();

                // Pokaż login
                using (var loginForm = new Forms.LoginForm(db))
                {
                    var result = loginForm.ShowDialog();
                    
                    if (result == DialogResult.OK)
                    {
                        // Uruchom dashboard
                        var dashboard = new Forms.DashboardForm(loginForm.LoggedInUser, db);
                        Application.Run(dashboard);
                        
                        // Po zamknięciu dashboardu, wróć na początek pętli
                    }
                    else
                    {
                        // Anulowano logowanie - wyjdź
                        break;
                    }
                }
            }
        }
    }
}