using System;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Services
{
    public interface ILogService
    {
        void Initialize(Action<string> logCallback);
        void Log(string message);
        void LogSuccess(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogInfo(string message);
        void Clear();
    }
}