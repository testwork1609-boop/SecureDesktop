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
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                // Utwórz foldery
                foreach (var dir in new[] { "Database", "Backup", "Logs" })
                {
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }

                // Inicjalizacja bazy JSON
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "database.json");
                var db = new Database.DatabaseInitializer(dbPath);
                db.Initialize();

                // Uruchom aplikację
                Application.Run(new Forms.LoginForm(db));
            }
            catch (Exception ex)
            {
                File.WriteAllText("fatal_error.log", ex.ToString());
                MessageBox.Show("Blad: " + ex.Message, "SecureDesktop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}