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

namespace Revit26_Plugin.DwgToDetailLines.V014.Commands
{
    /// <summary>
    /// Entry point for the DWG to Detail Lines tool.
    /// Enforces Project + Drafting View context and launches the UI.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DwgToDetailLinesCommand : IExternalCommand
    {
        /// <summary>Revit entry point; guarded by <see cref="ToolGuard"/>.</summary>
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
                TaskDialog.Show("DWG to Detail Lines", message);
                return Result.Cancelled;
            }

            var view = new DwgToDetailLinesWindow(uiApp);
            new System.Windows.Interop.WindowInteropHelper(view).Owner = uiApp.MainWindowHandle;
            view.Show();

            return Result.Succeeded;
        }
    }
}


