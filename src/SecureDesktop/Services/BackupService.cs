using System;
using System.IO;

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
                
                return destPath;
            }
            catch (Exception ex)
            {
                throw new Exception("Backup failed: " + ex.Message, ex);
            }
        }
    }
}