// ==============================================
// File: WorksetCreatorTool.cs
// Layer: tool root (namespace Revit26_Plugin_Revised.WorksetCreator.V015)
// Workset Creator V015.
//
// The tool's own name and version, so this folder compiles and runs with no
// edit to Shared/Services/ToolCatalog.cs. Every type under
// Revit26_Plugin_Revised.WorksetCreator.V015 resolves it without a using directive.
//
// Adding a "Workset Creator" entry to ToolCatalog later is still worth doing —
// ToolGuard then decorates the window title with the version and names this
// tool in its error dialog and daily log — but nothing in this folder depends
// on it. Keep the two in step if you add it.
// ==============================================

namespace Revit26_Plugin_Revised.WorksetCreator.V015
{
    internal static class WorksetCreatorTool
    {
        /// <summary>Display name — TaskDialog titles, error dialogs.</summary>
        public const string Name = "Workset Creator";

        /// <summary>Semantic version (MAJOR.MINOR.PATCH), as ToolCatalog would carry it.</summary>
        public const string Version = "15.0.0";

        /// <summary>Legacy build tag this folder and namespace carry.</summary>
        public const string BuildTag = "V015";

        /// <summary>"Workset Creator — v15.0.0", used as the report dialog's footer.</summary>
        public static string Title => $"{Name} — v{Version}";
    }
}
