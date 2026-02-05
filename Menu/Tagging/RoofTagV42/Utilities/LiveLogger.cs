using System;

namespace Revit26.RoofTagV42.Utilities
{
    public class LiveLogger : ILiveLogger
    {
        public event EventHandler<string> LogMessageReceived;

        public void Log(string message) => LogMessageReceived?.Invoke(this, message);
        public void LogInfo(string message) => Log($"[INFO] {message}");
        public void LogWarning(string message) => Log($"[WARN] {message}");
        public void LogError(string message) => Log($"[ERROR] {message}");
        public void LogSuccess(string message) => Log($"[SUCCESS] {message}");
    }

    public interface ILiveLogger
    {
        event EventHandler<string> LogMessageReceived;
        void Log(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogSuccess(string message);
    }
}