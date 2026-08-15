// ==============================================
// File: LaunchCommand.cs
// Layer: Commands
// Namespace: Revit26_Plugin.DwgToDetailLines.V009.Commands
// ==============================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DwgToDetailLines.V010.Helpers;
using Revit26_Plugin.DwgToDetailLines.V010.Views;

namespace Revit26_Plugin.DwgToDetailLines.V010.Commands
{
    /// <summary>
    /// Entry point for the DWG to Detail Lines tool.
    /// Enforces Project + Drafting View context and launches the UI.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LaunchCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            // --------------------------------------------------
            // HARD RULE: Project document + Drafting View ONLY
            // --------------------------------------------------
            if (!RevitContextValidator.IsDraftingViewInProject(uiApp, out message))
            {
                TaskDialog.Show("DWG to Detail Lines", message);
                return Result.Cancelled;
            }

            // --------------------------------------------------
            // Launch WPF window (modeless-style, Close only)
            // --------------------------------------------------
            var view = new DwgToDetailLinesView(uiApp);
            view.Show();

            return Result.Succeeded;
        }
    }
}
