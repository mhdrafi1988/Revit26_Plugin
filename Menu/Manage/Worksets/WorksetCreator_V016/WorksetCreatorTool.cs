// ==============================================
// File: WorksetCreatorTool.cs
// Layer: tool root (namespace Revit26_Plugin.WorksetCreator.V016)
// Workset Creator V016.
//
// The tool's own name and version, so this folder compiles and runs with no
// edit to Shared/Services/ToolCatalog.cs. Every type under
// Revit26_Plugin.WorksetCreator.V016 resolves it without a using directive.
//
// Adding / bumping a "Workset Creator" entry in ToolCatalog is still worth
// doing — ToolGuard then decorates the window title with the version and names
// this tool in its error dialog and daily log — but nothing in this folder
// depends on it. Keep the two in step if you do.
// ==============================================

namespace Revit26_Plugin.WorksetCreator.V016
{
    internal static class WorksetCreatorTool
    {
        /// <summary>Display name — TaskDialog titles, error dialogs.</summary>
        public const string Name = "Workset Creator";

        /// <summary>Semantic version (MAJOR.MINOR.PATCH), as ToolCatalog would carry it.</summary>
        public const string Version = "16.0.0";

        /// <summary>Legacy build tag this folder and namespace carry.</summary>
        public const string BuildTag = "V016";

        /// <summary>"Workset Creator — v16.0.0", used as the report dialog's footer.</summary>
        public static string Title => $"{Name} — v{Version}";
    }
}
