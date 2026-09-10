// ==============================================
// File: RevitContextValidator.cs
// Layer: Helpers
// ==============================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.DwgToDetailLines.V002.Helpers
{
    /// <summary>
    /// Centralized validation for Revit execution context.
    /// This tool only runs inside a Project document, on a Drafting View.
    /// </summary>
    public static class RevitContextValidator
    {
        public static bool IsDraftingViewInProject(
            UIApplication uiApp,
            out string message)
        {
            Document doc = uiApp?.ActiveUIDocument?.Document;

            if (doc == null)
            {
                message = "No active Revit document.";
                return false;
            }

            if (doc.IsFamilyDocument)
            {
                message = "This tool only runs in a Project document, not the Family Editor.";
                return false;
            }

            View activeView = uiApp.ActiveUIDocument.ActiveView;

            if (activeView == null || activeView.ViewType != ViewType.DraftingView)
            {
                string activeName = activeView?.Name ?? "Unknown";
                message =
                    $"This tool only runs in a Drafting View.\n\n" +
                    $"Active view \"{activeName}\" is not supported.\n" +
                    $"Open a Drafting View and relaunch.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
