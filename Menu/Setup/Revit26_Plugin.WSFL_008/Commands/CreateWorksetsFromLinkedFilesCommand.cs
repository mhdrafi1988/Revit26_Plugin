using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WSFL_008.ViewModels;
using Revit26_Plugin.WSFL_008.Views;

namespace Revit26_Plugin.WSFL_008.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateWorksetsFromLinkedFiles : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null || doc.IsFamilyDocument || !doc.IsWorkshared)
            {
                TaskDialog.Show(
                    "WSFL 008",
                    "Please open a workshared project file (not a family).");
                return Result.Cancelled;
            }

            var vm = new WorksetsViewModel(commandData);
            var window = new WorksetSelectorWindow(vm);
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
