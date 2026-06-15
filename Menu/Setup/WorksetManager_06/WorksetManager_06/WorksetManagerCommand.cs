using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WorksetManager.V06.ViewModels;
using Revit26_Plugin.WorksetManager.V06.Views;

namespace Revit26_Plugin.WorksetManager.V06
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateWorksetsFromLinkedFilesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;

            if (doc == null || !doc.IsWorkshared)
            {
                TaskDialog.Show("Workset Manager 04",
                    "This command requires an active workshared document.");
                return Result.Cancelled;
            }

            var viewModel = new WorksetsViewModel(commandData);
            var window    = new WorksetSelectorWindow(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
