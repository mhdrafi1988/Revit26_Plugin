using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WSAV03.Views;

namespace Revit26_Plugin.WSAV03
{
    [Transaction(TransactionMode.Manual)]
    public class CreateWorksetsFromLinkedFilesv03 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                             ref string message,
                             ElementSet elements)
        {
            var window = new WorksetSelectorWindow(commandData);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
