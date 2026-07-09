using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Interop;
using Revit26_Plugin.DetailLIneDimensions.V005.Services;
using Revit26_Plugin.DetailLIneDimensions.V005.ViewModels;
using Revit26_Plugin.DetailLIneDimensions.V005.Views;

namespace Revit26_Plugin.DetailLIneDimensions.V005.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DetailLineDimensionsCommand : IExternalCommand
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

                if (!ViewValidationService.ValidatePlanView(uiApp.ActiveUIDocument.ActiveView, out string reason))
                {
                    TaskDialog.Show("Detail Line Dimensions", reason);
                    return Result.Cancelled;
                }

                var vm = new DetailLineDimensionsViewModel(uiApp);
                var window = new DetailLineDimensionsWindow
                {
                    DataContext = vm
                };

                // Application.Current.MainWindow is unreliable inside Revit's process —
                // set the owner via the real Win32 handle instead. This is also what
                // prevents the "Cannot set Owner property" crash seen in other tools.
                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;

                window.Show();
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
