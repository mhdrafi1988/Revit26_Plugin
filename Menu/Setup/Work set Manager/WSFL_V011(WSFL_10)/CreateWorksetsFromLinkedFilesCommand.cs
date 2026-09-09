using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WSFL.V011.ViewModels;
using Revit26_Plugin.WSFL.V011.Views;

namespace Revit26_Plugin.WSFL.V011.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateWorksetsFromLinkedFiles : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                return ExecuteInternal(commandData, ref message, elements);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("WSFL 009", $"An unexpected error occurred: {ex.Message}");
                return Result.Failed;
            }
        }

        private Result ExecuteInternal(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null || doc.IsFamilyDocument || !doc.IsWorkshared)
            {
                TaskDialog.Show(
                    "WSFL 009",
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