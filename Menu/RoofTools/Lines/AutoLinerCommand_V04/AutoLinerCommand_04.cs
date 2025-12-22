using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoLiner_V04.Views;

namespace Revit26_Plugin.AutoLiner_V04.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoLinerCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            Autodesk.Revit.DB.ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
                return Result.Failed;

            AutoLinerWindow window = new AutoLinerWindow(uiDoc);
            window.Show();

            return Result.Succeeded;
        }
    }
}
