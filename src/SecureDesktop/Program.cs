using System;
using System.IO;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Forms;

namespace SecureDesktop
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Zapisz błędy do pliku
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                File.WriteAllText("crash.log", e.ExceptionObject.ToString());
            };

            try
            {
                // Utwórz foldery
                string[] dirs = { "Database", "Backup", "Logs" };
                foreach (var dir in dirs)
                {
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }

                // Podstawowe ustawienia
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Baza danych
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "SecureDesktop.db");
                var dbInitializer = new DatabaseInitializer(dbPath);
                dbInitializer.Initialize();

                // Uruchom okno logowania
                Application.Run(new LoginForm(dbInitializer.GetConnectionString()));
            }
            catch (Exception ex)
            {
                // Zapisz błąd
                File.WriteAllText("error.log", ex.ToString());
                
                // Pokaż błąd
                MessageBox.Show(
                    "Blad uruchamiania:\n" + ex.Message + "\n\nSzczegoly w pliku error.log",
                    "SecureDesktop - Blad",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}