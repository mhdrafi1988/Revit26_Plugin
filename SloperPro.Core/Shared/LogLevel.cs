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
        Success,

        /// <summary>Verbose, developer-facing detail (which face/curve was picked,
        /// intermediate clip/merge results, etc.) — additive value, existing switch
        /// expressions across tools fall through to their Info/default branch.</summary>
        Debug
    }
}
