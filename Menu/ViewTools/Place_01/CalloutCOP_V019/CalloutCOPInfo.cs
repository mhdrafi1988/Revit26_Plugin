namespace Revit26_Plugin.CalloutCOP.V019.Helpers
{
    /// <summary>
    /// Single source of truth for the tool's name and version. Bump
    /// <see cref="Version"/> here and every dialog, transaction name and
    /// external-event name follows.
    /// </summary>
    public static class CalloutCOPInfo
    {
        public const string ToolName = "Callout COP";
        public const string Version = "V19.0";
        public const string DisplayName = ToolName + " " + Version;
    }
}
