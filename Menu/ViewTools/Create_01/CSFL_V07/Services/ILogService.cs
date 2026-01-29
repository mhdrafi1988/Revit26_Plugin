namespace Revit26_Plugin.CSFL_V07.Services.Logging
{
    /// <summary>
    /// Contract for all logging implementations.
    /// Keeps services and orchestrators UI-agnostic.
    /// </summary>
    public interface ILogService
    {
        /// <summary>
        /// Informational message (normal workflow).
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Non-fatal issue that should be visible to the user.
        /// </summary>
        void Warning(string message);

        /// <summary>
        /// Fatal or blocking error.
        /// </summary>
        void Error(string message);
    }
}
