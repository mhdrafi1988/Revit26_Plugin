using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SheetViewArrange.V001.Infrastructure;
using Revit26_Plugin.SheetViewArrange.V001.UI.ViewModels;
using Revit26_Plugin.SheetViewArrange.V001.UI.Views;
using Revit26_Plugin.Shared.Services;
using System.Windows.Interop;

namespace Revit26_Plugin.SheetViewArrange.V001.Commands
{
    /// <summary>
    /// Opens Sheet View Arrange for the active sheet: re-orders the views already on it by
    /// detail number and lays them out as an evenly spaced, bottom-aligned reading table.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SheetViewArrangeCommand : IExternalCommand
    {
        /// <summary>Revit entry point — delegates to <see cref="ToolGuard.RunCommand"/>.</summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc?.Document == null)
            {
                TaskDialog.Show(ToolCatalog.SheetViewArrange.Title, "Open a project first.");
                return Result.Cancelled;
            }

            if (uiDoc.ActiveView is not ViewSheet sheet || sheet.IsPlaceholder)
            {
                TaskDialog.Show(ToolCatalog.SheetViewArrange.Title,
                    "Open the sheet you want to arrange, then run the tool again.");
                return Result.Cancelled;
            }

            var session = new RevitSheetArrangeSession(uiDoc.Document, sheet);
            SheetViewArrangeWindow window;
            try
            {
                window = new SheetViewArrangeWindow(new SheetViewArrangeViewModel(session));
            }
            catch
            {
                session.Dispose();
                throw;
            }

            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.Show(); // modeless — Refresh / Apply go through an ExternalEvent
            return Result.Succeeded;
        }
    }
}
