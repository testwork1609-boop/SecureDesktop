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

                // Inicjalizacja bazy
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "SecureDesktop.db");
                var dbInit = new Database.DatabaseInitializer(dbPath);
                dbInit.Initialize();

                // Uruchom aplikację
                Application.Run(new Forms.LoginForm(dbInit.GetConnectionString()));
            }
            catch (Exception ex)
            {
                File.WriteAllText("fatal_error.log", ex.ToString());
                MessageBox.Show("Blad: " + ex.Message + "\n\nZapisano w fatal_error.log", 
                    "SecureDesktop - Blad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}