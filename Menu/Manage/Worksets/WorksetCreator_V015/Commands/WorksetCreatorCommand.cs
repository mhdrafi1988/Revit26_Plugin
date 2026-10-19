// ==============================================
// File: WorksetCreatorCommand.cs
// Layer: Commands (thin Revit-side entry point only — CLAUDE.md)
// Workset Creator V015 — renamed lineage of Workset Manager V014.
// Changes vs V014:
//   - Execute routes through ToolGuard.RunCommand (suite-wide error net,
//     daily error log + TaskDialog) instead of its own try/catch + Logger.
//   - Tool name / version come from WorksetCreatorTool (this folder), so the
//     tool builds with no edit to Shared/Services/ToolCatalog.cs.
// Behaviour is V014's: workshared-document guard, modeless window owned by
// Revit's main window handle.
// ==============================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin_Revised.Shared.Services;
using Revit26_Plugin_Revised.WorksetCreator.V015.UI.ViewModels;
using Revit26_Plugin_Revised.WorksetCreator.V015.UI.Views;
using System.Windows.Interop;

namespace Revit26_Plugin_Revised.WorksetCreator.V015.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WorksetCreatorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(typeof(WorksetCreatorCommand), commandData, ref message, elements, ExecuteInternal);

        private Result ExecuteInternal(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;

            if (doc == null || !doc.IsWorkshared)
            {
                TaskDialog.Show(WorksetCreatorTool.Name,
                    "This command requires an active workshared document.");
                return Result.Cancelled;
            }

            var viewModel = new WorksetCreatorViewModel(commandData);
            var window = new WorksetCreatorWindow(viewModel);

            // Parent to Revit's actual main window handle. System.Windows.Application.Current
            // is not guaranteed to exist inside Revit's process, so System.Windows.Application.Current.MainWindow
            // can NullReferenceException, or resolve to the wrong window and break
            // WindowStartupLocation="CenterOwner" / z-order against Revit.
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            window.Show();
            return Result.Succeeded;
        }
    }
}
