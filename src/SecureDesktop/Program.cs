using System;
using System.IO;
using System.Windows.Forms;
using SecureDesktop.Database;
using SecureDesktop.Forms;
using Serilog;

namespace SecureDesktop
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ConfigureLogging();
            
            try
            {
                Log.Information("SecureDesktop application starting");
                
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "SecureDesktop.db");
                var dbInitializer = new DatabaseInitializer(dbPath);
                dbInitializer.Initialize();
                
                Log.Information("Database initialized successfully");
                
                Application.Run(new LoginForm(dbInitializer.GetConnectionString()));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Unhandled exception during startup");
                MessageBox.Show(
                    "Krytyczny błąd podczas uruchamiania aplikacji.",
                    "SecureDesktop - Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureLogging()
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "SecureDesktop-.log");
            
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30
                )
                .CreateLogger();
        }
    }
}