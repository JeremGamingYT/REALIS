using System;
using System.IO;

namespace REALIS.Core
{
    /// <summary>
    /// Simple logger that writes messages to a local file.
    /// </summary>
    internal static class Logger
    {
        private static readonly string LogPath = "TrafficAI.log";

        public static void Info(string message)
        {
            try
            {
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

