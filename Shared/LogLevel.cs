namespace Revit26_Plugin.Shared.Models
{
    /// <summary>
    /// Shared log severity levels used by log panels and converters
    /// across all tools (WorksetManager, AutoSlopeByPoint, RoofRidgeLines, etc).
    /// Single canonical enum — LoggingLevel is deprecated.
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success
    }
}
