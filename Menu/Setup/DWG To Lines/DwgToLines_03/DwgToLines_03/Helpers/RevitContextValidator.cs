// ==============================================
// File: RevitContextValidator.cs
// Layer: Helpers
// ==============================================

using Autodesk.Revit.UI;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Helpers
{
    /// <summary>
    /// Centralized validation for Revit execution context.
    /// </summary>
    public static class RevitContextValidator
    {
        /// <summary>
        /// Ensures the command is executed inside the Family Editor.
        /// </summary>
        public static bool IsFamilyEditor(
            UIApplication uiApp,
            out string message)
        {
            if (uiApp?.ActiveUIDocument?.Document == null)
            {
                message = "No active Revit document.";
                return false;
            }

            if (!uiApp.ActiveUIDocument.Document.IsFamilyDocument)
            {
                message = "This tool can only be run inside the Family Editor.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
