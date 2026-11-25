using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionToSheet.V001.UI;
using Revit26_Plugin.Shared.Services;
using System.Windows.Interop;

namespace Revit26_Plugin.SectionToSheet.V001.Commands
{
    /// <summary>
    /// Opens Section To Sheet for the active plan view: every section visible in the plan is placed
    /// on sheets (already-placed ones are moved). The window is modal, so Place runs in this
    /// command's API context.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SectionToSheetCommand : IExternalCommand
    {
        /// <summary>Revit entry point — delegates to <see cref="ToolGuard.RunCommand"/>.</summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteUnguarded);

        private Result ExecuteUnguarded(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc?.Document == null)
            {
                TaskDialog.Show(ToolCatalog.SectionToSheet.Title, "Open a project first.");
                return Result.Cancelled;
            }
            if (uiDoc.Document.IsFamilyDocument)
            {
                TaskDialog.Show(ToolCatalog.SectionToSheet.Title, "This tool works in a project, not a family.");
                return Result.Cancelled;
            }
            if (uiDoc.ActiveView is not ViewPlan plan || plan.IsTemplate
                || (plan.ViewType != ViewType.FloorPlan && plan.ViewType != ViewType.CeilingPlan
                    && plan.ViewType != ViewType.AreaPlan && plan.ViewType != ViewType.EngineeringPlan))
            {
                TaskDialog.Show(ToolCatalog.SectionToSheet.Title,
                    "Open the plan view whose sections you want to place (floor, ceiling, area or structural plan), then run the tool again.");
                return Result.Cancelled;
            }

            var window = new SectionToSheetWindow(new SectionToSheetViewModel(uiDoc, plan.Id));
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.ShowDialog(); // modal — Place runs inside this command
            return Result.Succeeded;
        }
    }
}
