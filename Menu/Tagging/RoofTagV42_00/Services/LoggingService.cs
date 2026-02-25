using System;
using System.IO;

namespace Revit26.RoofTagV42.Services
{
    public static class LoggingService
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Revit26_RoofTagV42_System.log");

        public static void LogInfo(string message) => Log("INFO", message);
        public static void LogWarning(string message) => Log("WARN", message);
        public static void LogError(string message) => Log("ERROR", message);
        public static void LogDebug(string message) => Log("DEBUG", message);

        private static void Log(string level, string message)
        {
            try
            {
                File.AppendAllText(LogFilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Silent fail for system logging
            }
        }

        public static void ClearLog()
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    File.Delete(LogFilePath);
                }
            }
            catch
            {
                // Silent fail
            }
        }
    }
}