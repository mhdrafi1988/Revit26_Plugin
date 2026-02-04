using System;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Services
{
    public class LogService : ILogService
    {
        private Action<string> _logCallback;

        public void Initialize(Action<string> logCallback)
        {
            _logCallback = logCallback;
        }

        public void Log(string message)
        {
            _logCallback?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        public void LogSuccess(string message)
        {
            Log($"? {message}");
        }

        public void LogWarning(string message)
        {
            Log($"?? {message}");
        }

        public void LogError(string message)
        {
            Log($"? {message}");
        }

        public void LogInfo(string message)
        {
            Log($"?? {message}");
        }

        public void Clear()
        {
            // This will be handled by the ViewModel clearing the collection
        }
    }
}