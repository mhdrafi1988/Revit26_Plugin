using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin_Revised.DwgManager.V002.UI.ViewModels;
using Revit26_Plugin_Revised.DwgManager.V002.UI.Views;
using Revit26_Plugin_Revised.Shared.Services;

namespace Revit26_Plugin_Revised.DwgManager.V002.Commands
{
    /// <summary>
    /// Opens the DWG Manager window (single instance per Revit session). Running it again brings the
    /// open window forward, or reopens it for the active document if the user switched documents.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DwgManagerCommand : IExternalCommand
    {
        private static DwgManagerWindow _window;

        /// <inheritdoc/>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc?.Document == null)
            {
                TaskDialog.Show(ToolCatalog.DwgManager.Title, "Open a project first.");
                return Result.Cancelled;
            }

            if (_window != null)
            {
                if (_window.ViewModel.IsFor(uidoc.Document))
                {
                    if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
                    _window.Activate();
                    return Result.Succeeded;
                }

                // Opened for another document: replace it with a window for the active one.
                _window.Close();
            }

            var viewModel = new DwgManagerViewModel(uidoc);
            var window = new DwgManagerWindow(viewModel);
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_window, window)) _window = null;
            };

            _window = window;
            window.Show();
            return Result.Succeeded;
        }
    }
}
