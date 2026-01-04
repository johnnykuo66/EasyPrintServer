using System;
using System.IO;

namespace EasyPrintServer
{
    public static class Logger
    {
        private static readonly object _lock = new object();

        public static string LogDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                         "EasyPrintServer", "Logs");

        public static string LogFilePath =>
            Path.Combine(LogDirectory, "EasyPrintServer.log");

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never crash the app because logging failed
            }
        }
    }
}
