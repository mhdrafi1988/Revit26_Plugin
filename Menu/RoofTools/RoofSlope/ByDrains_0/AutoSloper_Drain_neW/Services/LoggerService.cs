// =======================================================
// File: Services/Implementations/LoggerService.cs
// Description: Logging service implementation
// =======================================================

using Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces;
using System;
using System.Text;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Implementations
{
    public class LoggerService : ILoggerService
    {
        private readonly StringBuilder _logBuilder = new StringBuilder();
        public event Action<string> LogMessageAdded;

        private void AddLog(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedMessage = $"[{timestamp}] [{level}] {message}";

            lock (_logBuilder)
            {
                _logBuilder.AppendLine(formattedMessage);
            }

            LogMessageAdded?.Invoke(formattedMessage);
        }

        public void Log(string message) => AddLog("INFO", message);
        public void LogInfo(string message) => AddLog("INFO", message);
        public void LogWarning(string message) => AddLog("WARN", message);
        public void LogError(string message) => AddLog("ERROR", message);

        public void Clear()
        {
            lock (_logBuilder)
            {
                _logBuilder.Clear();
            }
        }

        public string GetLogText()
        {
            lock (_logBuilder)
            {
                return _logBuilder.ToString();
            }
        }
    }
}