using System;

namespace RoofTagV3.Utilities
{
    public class LiveLogger
    {
        public event EventHandler<string> LogMessageReceived;

        public void Log(string message)
        {
            LogMessageReceived?.Invoke(this, message);
        }

        public void LogInfo(string message) => Log($"[INFO] {message}");
        public void LogWarning(string message) => Log($"[WARN] {message}");
        public void LogError(string message) => Log($"[ERROR] {message}");
        public void LogSuccess(string message) => Log($"[SUCCESS] {message}");
    }
}