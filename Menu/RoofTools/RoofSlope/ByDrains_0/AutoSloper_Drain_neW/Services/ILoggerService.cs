// =======================================================
// File: Services/Interfaces/ILoggerService.cs
// Description: Service contract for logging
// =======================================================

using System;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces
{
    /// <summary>
    /// Provides logging functionality
    /// </summary>
    public interface ILoggerService
    {
        event Action<string> LogMessageAdded;

        void Log(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        void Clear();
        string GetLogText();
    }
}