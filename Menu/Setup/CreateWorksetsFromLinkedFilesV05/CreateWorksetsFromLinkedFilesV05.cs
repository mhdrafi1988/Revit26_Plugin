using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Revit26_Plugin.WSA_V05.Views;

namespace Revit26_Plugin.WSA_V05.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateWorksetsFromLinkedFilesV05 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            Autodesk.Revit.DB.ElementSet elements)
        {
            if (commandData?.Application?.ActiveUIDocument == null)
                return Result.Cancelled;

            var window = new WorksetSelectorWindow(commandData);
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
