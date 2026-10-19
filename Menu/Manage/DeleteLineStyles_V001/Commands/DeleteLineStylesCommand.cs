using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DeleteLineStyles.V001.UI.ViewModels;
using Revit26_Plugin.DeleteLineStyles.V001.UI.Views;
using Revit26_Plugin.Shared.Services;
using System.Windows.Interop;

namespace Revit26_Plugin.DeleteLineStyles.V001.Commands
{
    /// <summary>
    /// Opens the Delete Line Styles window, which lists the project's custom line styles and
    /// deletes the unused ones (or, optionally, all of them).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DeleteLineStylesCommand : IExternalCommand
    {
        /// <inheritdoc/>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null)
            {
                TaskDialog.Show(ToolCatalog.DeleteLineStyles.Title, "Open a project first.");
                return Result.Cancelled;
            }

            if (doc.IsReadOnly)
            {
                TaskDialog.Show(ToolCatalog.DeleteLineStyles.Title, "The active document is read-only.");
                return Result.Cancelled;
            }

            var viewModel = new DeleteLineStylesViewModel(commandData);
            var window = new DeleteLineStylesWindow(viewModel);
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.Show();
            return Result.Succeeded;
        }
    }
}
