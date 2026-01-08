// File: LogCategory.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Logging
//
// Responsibility:
// - Defines categorized log types for diagnostics
// - Used by services, execution engine, and UI

namespace Revit26_Plugin.RoofRidgeLines_V06.Logging
{
    /// <summary>
    /// Categorizes log entries by failure or execution type.
    /// </summary>
    public enum LogCategory
    {
        Info,
        Warning,
        GeometryFailure,
        UnsupportedRoof,
        UserCancel,
        ApiException
    }
}
