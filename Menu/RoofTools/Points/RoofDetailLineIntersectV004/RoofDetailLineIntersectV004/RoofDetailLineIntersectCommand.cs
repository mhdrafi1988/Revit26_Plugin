using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofDetailLineIntersect.V004.Filters;

namespace Revit26_Plugin.RoofDetailLineIntersect.V004
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofDetailLineIntersectCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc      = commandData.Application.ActiveUIDocument;
            Document   doc        = uiDoc.Document;
            View       activeView = doc.ActiveView;

            // ── Guard: must be a plan view ──────────────────────────────────
            if (activeView.ViewType != ViewType.FloorPlan  &&
                activeView.ViewType != ViewType.CeilingPlan &&
                activeView.ViewType != ViewType.AreaPlan)
            {
                TaskDialog.Show("Roof Detail Line Intersect",
                    "Active view must be a Plan View.\nPlease switch to a floor plan and retry.");
                return Result.Cancelled;
            }

            // ── Step 1: select one Roof ──────────────────────────────────────
            Reference roofRef;
            try
            {
                roofRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select one Roof element");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            Element roofElem = doc.GetElement(roofRef.ElementId);
            if (roofElem is not FootPrintRoof roof)
            {
                TaskDialog.Show("Roof Detail Line Intersect", "Selected element is not a FootPrint Roof.");
                return Result.Cancelled;
            }

            // ── Step 2: multi-select Detail Lines ───────────────────────────
            IList<Reference> lineRefs;
            try
            {
                lineRefs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    new DetailLineSelectionFilter(),
                    "Multi-select Detail Lines, then press Finish");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (lineRefs == null || lineRefs.Count == 0)
            {
                TaskDialog.Show("Roof Detail Line Intersect", "No Detail Lines selected.");
                return Result.Cancelled;
            }

            var detailLines = lineRefs
                .Select(r => doc.GetElement(r.ElementId))
                .OfType<DetailLine>()
                .ToList();

            // ── Build VM, create ExternalEvent here (valid Revit API context) ─
            var vm      = new RoofDetailLineIntersectViewModel(uiDoc, doc, roof, detailLines);
            var handler = new PlacePointsEventHandler(vm);
            var exEvent = ExternalEvent.Create(handler);   // ← only legal here, not in WPF button click
            vm.SetExternalEvent(exEvent);

            // ── Launch window ────────────────────────────────────────────────
            var window = new RoofDetailLineIntersectWindow(vm);
            window.Show();

            return Result.Succeeded;
        }
    }
}
