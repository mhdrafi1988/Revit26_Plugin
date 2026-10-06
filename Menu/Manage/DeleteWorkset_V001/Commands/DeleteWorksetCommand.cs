using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DeleteWorkset.V001.UI.ViewModels;
using Revit26_Plugin.DeleteWorkset.V001.UI.Views;
using Revit26_Plugin.Shared.Services;
using Revit26_Plugin.Utilities;
using System.Windows.Interop;

namespace Revit26_Plugin.DeleteWorkset.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DeleteWorksetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;

            if (doc == null || !doc.IsWorkshared)
            {
                TaskDialog.Show("Delete Workset",
                    "This command requires an active workshared document.");
                return Result.Cancelled;
            }

            var viewModel = new DeleteWorksetViewModel(commandData);
            var window    = new DeleteWorksetWindow(viewModel);

            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            window.Show();
            return Result.Succeeded;
        }
    }
}
