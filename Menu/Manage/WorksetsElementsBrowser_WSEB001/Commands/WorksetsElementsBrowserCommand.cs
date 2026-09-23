using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB001.UI.ViewModels;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB001.UI.Views;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB001.Commands
{
    /// <summary>
    /// Ribbon entry point for the Worksets &amp; Elements Browser tool. Opens a
    /// modeless window (matches this project's window-lifecycle convention)
    /// listing every workset, category and type with tri-state checkboxes.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WorksetsElementsBrowserCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null)
                {
                    message = "No active document.";
                    return Result.Failed;
                }

                var viewModel = new WorksetsElementsBrowserViewModel(uiDoc);
                var window = new WorksetsElementsBrowserWindow
                {
                    DataContext = viewModel
                };

                viewModel.CloseRequested += window.Close;

                window.Show(); // modeless — matches our Close-only modeless dialog convention

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
