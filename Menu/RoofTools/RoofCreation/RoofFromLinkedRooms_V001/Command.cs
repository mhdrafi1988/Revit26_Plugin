using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.RoofFromLinkedRooms.V001
{
    /// <summary>
    /// Creates one flat FootPrintRoof per selected room of a linked model. Each room's boundary
    /// is transformed into host coordinates; inner room loops become openings; the host level
    /// is auto-matched by elevation. Works from any view.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        /// <summary>Revit entry point — delegates to <see cref="ToolGuard.RunCommand"/>.</summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;

            if (doc.IsReadOnly)
            {
                TaskDialog.Show(ToolCatalog.RoofFromLinkedRooms.Name, "The document is read-only, so roofs cannot be created.");
                return Result.Cancelled;
            }

            var handler = new RunCreateRoofsExternalEventHandler();
            var externalEvent = ExternalEvent.Create(handler);

            var viewModel = new MainViewModel(doc, handler, externalEvent);
            handler.ViewModel = viewModel;

            var window = new RoofFromLinkedRoomsWindow { DataContext = viewModel };
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.Show();

            return Result.Succeeded;
        }
    }
}
