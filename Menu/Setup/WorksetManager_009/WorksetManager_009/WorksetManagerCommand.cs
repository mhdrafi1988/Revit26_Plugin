using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WorksetManager.V009.ViewModels;
using Revit26_Plugin.WorksetManager.V009.Views;
using System.Windows.Interop;

namespace Revit26_Plugin.WorksetManager.V009
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
                TaskDialog.Show("Workset Manager 009",
                    "This command requires an active workshared document.");
                return Result.Cancelled;
            }

            var viewModel = new WorksetsViewModel(commandData);
            var window = new WorksetSelectorWindow(viewModel);

            // ✅ Set owner using Revit's main window handle (fixes the "Cannot set Owner property" error)
            new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
