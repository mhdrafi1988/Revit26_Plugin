// File: UILogEntry.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Logging
//
// Responsibility:
// - UI-friendly log entry model
// - No logging framework dependency
// - Safe for ObservableCollection binding

using System;

namespace Revit26_Plugin.RoofRidgeLines_V06.Logging
{
    /// <summary>
    /// Represents a single log entry shown in the UI.
    /// </summary>
    public class UILogEntry
    {
        public DateTime Timestamp { get; }

        public LogCategory Category { get; }

        public string Message { get; }

        public UILogEntry(LogCategory category, string message)
        {
            Timestamp = DateTime.Now;
            Category = category;
            Message = message;
        }
    }
}
