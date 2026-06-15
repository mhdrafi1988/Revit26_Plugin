using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DwgSymbolicConverter_V02.Views;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class LaunchCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;

            if (!Helpers.RevitContextValidator.IsFamilyEditor(uiApp, out message))
                return Result.Failed;

            var view = new DwgSymbolicConverterView(uiApp);
            view.ShowDialog();

            return Result.Succeeded;
        }
    }
}
