using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WorksetManager.V010.ViewModels;
using Revit26_Plugin.WorksetManager.V010.Views;
using System.Windows.Interop;

namespace Revit26_Plugin.WorksetManager.V010
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateWorksetsFromLinkedFiles : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;

            if (doc == null || !doc.IsWorkshared)
            {
                TaskDialog.Show("Workset Manager",
                    "This command requires an active workshared document.");
                return Result.Cancelled;
            }

            var viewModel = new WorksetsViewModel(commandData);
            var window = new WorksetSelectorWindow(viewModel);

            // Parent to Revit's actual main window handle. System.Windows.Application.Current
            // is not guaranteed to exist inside Revit's process, so Application.Current.MainWindow
            // can NullReferenceException, or resolve to the wrong window and break
            // WindowStartupLocation="CenterOwner" / z-order against Revit.
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}