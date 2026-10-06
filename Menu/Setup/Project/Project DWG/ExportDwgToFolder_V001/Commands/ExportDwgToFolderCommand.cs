using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.ExportDwgToFolder.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.ExportDwgToFolder.V001.UI.ViewModels;
using Revit26_Plugin.ExportDwgToFolder.V001.UI.Views;

namespace Revit26_Plugin.ExportDwgToFolder.V001.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportDwgToFolderCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;

                var handler = new DwgExportEventHandler();
                var externalEvent = ExternalEvent.Create(handler);

                var viewModel = new ExportDwgToFolderViewModel(handler, externalEvent);
                var window = new ExportDwgToFolderWindow(viewModel);
                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
                window.Show();

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Export DWG to Folder", $"Could not open the tool:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
