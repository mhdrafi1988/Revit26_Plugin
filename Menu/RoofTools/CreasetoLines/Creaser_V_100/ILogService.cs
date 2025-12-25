using System;

namespace Revit26_Plugin.Creaser_V100.Services
{
    /// <summary>
    /// Central logging contract used by all services and view models.
    /// </summary>
    public interface ILogService
    {
        void Info(string source, string message);
        void Warning(string source, string message);
        void Error(string source, string message);

        /// <summary>
        /// Creates a scope that automatically logs entry and exit.
        /// </summary>
        IDisposable Scope(string source, string scopeName);
    }
}
