using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofFromDetailLines.V007
{
    /// <summary>
    /// V007 — Creates flat FootPrintRoofs from detail lines/arcs already sketched in the
    /// active plan view, at the view's attached level. Confirmed spec:
    /// - Must be launched from a Plan view; exits with a message otherwise.
    /// - User selects detail lines/arcs BEFORE opening the tool; the pre-selection is
    ///   silently filtered down to DetailLine/DetailArc elements (Detail Items category
    ///   curves) — anything else in the selection is ignored.
    /// - Nested loops pair as (outer boundary, opening) two levels at a time, from the
    ///   outside in; a third level starts a fresh outer boundary of its own.
    /// - Open chains with exactly 2 free endpoints are auto-closed with a straight
    ///   connector line. Overlapping/near-corner lines are resolved by extending/
    ///   trimming to a clean junction (same mechanism for both cases).
    /// - Unresolvable groups are skipped (Warning), never blocking the rest of the run.
    /// - No slope — flat roofs only.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;
            var activeView = doc.ActiveView;

            // ── Plan-view guard (confirmed spec) ────────────────────────────────────
            if (activeView == null || activeView.ViewType != ViewType.FloorPlan)
            {
                TaskDialog.Show("Roof From Detail Lines",
                    "This tool must be run from a Plan view.\n\n" +
                    $"Active view '{activeView?.Name}' is of type '{activeView?.ViewType}'. " +
                    "Switch to a Floor Plan view, select your detail lines, and run the tool again.");
                return Result.Cancelled;
            }

            var level = doc.GetElement(activeView.GenLevel?.Id ?? ElementId.InvalidElementId) as Level;
            if (level == null)
            {
                TaskDialog.Show("Roof From Detail Lines",
                    $"The active view '{activeView.Name}' has no associated Level. Cannot determine where to create roofs.");
                return Result.Cancelled;
            }

            // ── Pre-selection filter: DetailLine/DetailArc only, silently ───────────
            var selectedIds = uidoc.Selection.GetElementIds();
            var detailElements = new List<Element>();
            foreach (var id in selectedIds)
            {
                var el = doc.GetElement(id);

                // Keep only view-specific detail curves (DetailLine/DetailArc — category
                // OST_Lines, CurveElementType.DetailCurve). Silently drop anything else
                // in the pre-selection: model lines, dimensions, tags, other elements.
                if (el is CurveElement ce
                    && ce.CurveElementType == CurveElementType.DetailCurve
                    && ce.Category != null
                    && ce.Category.Id.Value == (long)BuiltInCategory.OST_Lines)
                {
                    detailElements.Add(el);
                }
            }

            if (detailElements.Count == 0)
            {
                TaskDialog.Show("Roof From Detail Lines",
                    "No detail lines or arcs were found in your selection.\n\n" +
                    "Select the detail lines/arcs that form your roof outline(s) before launching this tool.");
                return Result.Cancelled;
            }

            var handler = new RunCreateRoofsExternalEventHandler();
            var externalEvent = ExternalEvent.Create(handler);

            var viewModel = new MainViewModel(doc, activeView, level, detailElements, handler, externalEvent);
            handler.ViewModel = viewModel;

            var window = new RoofFromDetailLinesWindow { DataContext = viewModel };
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.Show();

            return Result.Succeeded;
        }
    }
}
