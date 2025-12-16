using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DtlLineDim_V03.ViewModels;
using Revit26_Plugin.DtlLineDim_V03.Views;

namespace Revit26_Plugin.DtlLineDim_V03.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DtlLineDimCommand_01 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                Document doc = uiApp.ActiveUIDocument.Document;

                if (doc.IsReadOnly)
                {
                    TaskDialog.Show("Error", "Document is read-only.");
                    return Result.Failed;
                }

                var vm = new DtlLineDimViewModel_V03(uiApp);
                var window = new DtlLineDimWindow
                {
                    DataContext = vm
                };

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }
    }
}
