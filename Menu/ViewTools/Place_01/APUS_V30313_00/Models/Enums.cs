namespace Revit26_Plugin.APUS_V313.Enums
{
    /// <summary>
    /// UI state for the main window
    /// </summary>
    public enum UiState
    {
        Idle,
        CollectingData,
        ReadyToPlace,
        Placing,
        Completed,
        Error,
        Cancelled
    }

    /// <summary>
    /// Progress state for placement operations
    /// </summary>
    public enum ProgressState
    {
        NotStarted,
        Running,
        Completed,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Validation state for placement parameters
    /// </summary>
    public enum PlacementValidationState
    {
        Valid,
        NoSections,
        NoTitleBlock,
        NoPlacementArea,
        InvalidParameters,
        GeneralError
    }

    /// <summary>
    /// Placement state filter options
    /// </summary>
    public enum PlacementFilterState
    {
        All,
        PlacedOnly,
        UnplacedOnly
    }

    /// <summary>
    /// Log level for logging messages
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}

namespace Revit26_Plugin.APUS_V313.Constants
{
    /// <summary>
    /// Shared parameter names
    /// </summary>
    public static class ParameterNames
    {
        public const string PlacementScope = "Placement_Scope";
    }

    /// <summary>
    /// Log level constants
    /// </summary>
    public static class LogLevelConstants
    {
        public const string Info = "Info";
        public const string Warning = "Warning";
        public const string Error = "Error";
    }
}