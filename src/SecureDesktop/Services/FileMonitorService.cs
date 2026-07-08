using System;
using System.IO;
using SecureDesktop.Utils;

namespace SecureDesktop.Services
{
    public class FileMonitorService
    {
        public class FileState
        {
            public string FilePath { get; set; }
            public long Size { get; set; }
            public DateTime ModifiedDate { get; set; }
            public string Hash { get; set; }

            public static FileState Capture(string path)
            {
                var info = new FileInfo(path);
                return new FileState
                {
                    FilePath = path,
                    Size = info.Length,
                    ModifiedDate = info.LastWriteTime,
                    Hash = SecurityHelper.ComputeFileHash(path)
                };
            }
        }

        public bool HasChanged(string filePath, FileState previousState)
        {
            if (!File.Exists(filePath)) return true;
            
            var currentState = FileState.Capture(filePath);
            
            return currentState.Size != previousState.Size ||
                   currentState.ModifiedDate != previousState.ModifiedDate ||
                   currentState.Hash != previousState.Hash;
        }
    }
}