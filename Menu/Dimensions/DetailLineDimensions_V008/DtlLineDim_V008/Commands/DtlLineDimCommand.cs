using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using Revit26_Plugin.DtlLineDim.V008.UI.ViewModels;
using Revit26_Plugin.DtlLineDim.V008.UI.Views;
using Revit26_Plugin.Utilities;

namespace Revit26_Plugin.DtlLineDim.V008.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.NotNeeded)]
    public class DtlLineDimCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument?.Document;

                if (doc == null)
                {
                    TaskDialog.Show("Error", "No active document.");
                    return Result.Failed;
                }

                if (doc.IsReadOnly)
                {
                    TaskDialog.Show("Error", "Document is read-only.");
                    return Result.Failed;
                }

                var vm = new DtlLineDimViewModel(uiApp);
                var window = new DtlLineDimWindow
                {
                    DataContext = vm
                };

                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
                window.Show();

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                Logger.Error("DtlLineDimCommand", ex);
                message = ex.ToString();
                return Result.Failed;
            }
        }
    }
}
