using System;
using System.IO;
using Serilog;

namespace SecureDesktop.Services
{
    public class BackupService
    {
        public string CreateBackup(string sourcePath, string backupRoot)
        {
            try
            {
                var dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                var timeFolder = DateTime.Now.ToString("HH-mm-ss");
                var backupDir = Path.Combine(backupRoot, dateFolder, timeFolder);
                
                Directory.CreateDirectory(backupDir);
                
                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(backupDir, fileName);
                
                File.Copy(sourcePath, destPath, false);
                
                Log.Information("Backup created: {Path}", destPath);
                return destPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Backup failed for: {Path}", sourcePath);
                throw;
            }
        }
    }
}