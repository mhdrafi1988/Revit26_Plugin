// ==============================================
// File: DwgToDetailLinesCommand.cs
// Layer: Commands
// Namespace: Revit26_Plugin.DwgToDetailLines.V014.Commands
// ==============================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DwgToDetailLines.V014.Infrastructure.Helpers;
using Revit26_Plugin.DwgToDetailLines.V014.UI.Views;
using Revit26_Plugin.Shared.Services;
using Revit26_Plugin.Utilities;

namespace Revit26_Plugin.DwgToDetailLines.V014.Commands
{
    /// <summary>
    /// Entry point for the DWG To DL(P) tool (V014).
    /// Enforces Project + Drafting View context and launches the UI.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DwgToDetailLinesCommand : IExternalCommand
    {
        /// <summary>Runs the tool; any unexpected error is logged and shown by <see cref="ToolGuard"/>.</summary>
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteInternal);

        private Result ExecuteInternal(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            if (!RevitContextValidator.IsDraftingViewInProject(uiApp, out message))
            {
                TaskDialog.Show("DWG To DL(P)", message);
                return Result.Cancelled;
            }

            var view = new DwgToDetailLinesWindow(uiApp);
            view.Show();

            return Result.Succeeded;
        }
    }
}


