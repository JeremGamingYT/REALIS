using System;
using System.IO;

namespace REALIS.Core
{
    /// <summary>
    /// Simple logger that writes messages to a local file.
    /// </summary>
    internal static class Logger
    {
        private static readonly string LogPath = "REALISS.log";

        private const long MaxLogSizeBytes = 5 * 1024 * 1024; // 5 Mo

        private static void EnsureLogSize()
        {
            try
            {
                if (File.Exists(LogPath))
                {
                    var info = new FileInfo(LogPath);
                    if (info.Length > MaxLogSizeBytes)
                    {
                        string archiveName = $"TrafficAI_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                        File.Move(LogPath, archiveName);
                    }
                }
            }
            catch { /* Ne pas interrompre l'application si la rotation échoue */ }
        }

        public static void Info(string message)
        {
            try
            {
                EnsureLogSize();
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] {message}\n");
            }
            catch { }
        }

        public static void Error(string message)
        {
            Info($"ERROR: {message}");
        }
    }
}

