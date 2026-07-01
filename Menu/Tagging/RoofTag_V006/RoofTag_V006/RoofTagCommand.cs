using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTag_V006.Helpers;
using Revit26_Plugin.Shared.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V006
{
    [Transaction(TransactionMode.Manual)]
    public class RoofTagCommand : IExternalCommand
    {
        // Duplicate proximity tolerance for INPUT points: 10 mm → feet
        private const double PointDedupTolFt = 10.0 / 304.8;

        public Result Execute(
            ExternalCommandData commandData,
            ref string          message,
            ElementSet          elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument    uiDoc = uiApp.ActiveUIDocument;
            Document      doc   = uiDoc.Document;

            // ── 1. Select roof ───────────────────────────────────────────
            RoofBase roof = SelectionHelper.SelectRoof(uiDoc);
            if (roof == null)
            {
                TaskDialog.Show("Roof Tag V006", "No roof selected.");
                return Result.Cancelled;
            }

            // ── 2. Ensure SlabShapeEditor is enabled ─────────────────────
            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor != null && !editor.IsEnabled)
            {
                using Transaction tx = new Transaction(doc, "Enable Slab Shape Editor");
                tx.Start();
                editor.Enable();
                tx.Commit();
            }

            // ── 3. Show settings window ──────────────────────────────────
            RoofTagWindow window = new RoofTagWindow(uiApp);
            if (window.ShowDialog() != true)
                return Result.Cancelled;

            RoofTagViewModel vm = (RoofTagViewModel)window.DataContext;
            vm.ResetCounters();

            // ── 4. Collect points ────────────────────────────────────────
            List<XYZ> rawPoints;

            if (vm.UseManualMode)
            {
                IList<Reference> refs = uiDoc.Selection.PickObjects(
                    ObjectType.PointOnElement,
                    "Select points on roof");
                rawPoints = refs.Select(r => r.GlobalPoint).ToList();
            }
            else
            {
                rawPoints = RoofTagGeometryHelper.GetExactShapeVertices(roof);
            }

            if (rawPoints.Count == 0)
            {
                TaskDialog.Show("Roof Tag V006", "No valid points found.");
                return Result.Cancelled;
            }

            // ── 5. Deduplicate input points at 10 mm ─────────────────────
            List<XYZ> points = DeduplicatePoints(rawPoints, PointDedupTolFt);

            vm.AddLog(new LogEntry(LogLevel.Info,
                $"Roof id {roof.Id} — {rawPoints.Count} raw pts → {points.Count} unique"));

            // ── 6. Place tags ─────────────────────────────────────────────
            using (Transaction tx = new Transaction(doc, "Place Roof Tags V006"))
            {
                tx.Start();

                foreach (XYZ pt in points)
                {
                    // Get face reference (best quality — equivalent to manual click)
                    RoofTagGeometryHelper.GetTaggingReferenceOnRoof(
                        roof, pt,
                        out Reference faceRef,
                        out XYZ projected);

                    // Use projected point if available, otherwise raw point
                    XYZ origin = projected ?? pt;

                    LogEntry result = RoofTaggingService.PlaceTag(doc, faceRef, roof, origin, vm);
                    vm.AddLog(result);
                }

                tx.Commit();
            }

            // ── 7. Re-show window with log results ───────────────────────
            // Window stays open so user can read the log and copy if needed.
            // (Window was already shown above for settings; we reuse the same
            //  instance so log entries are already bound and visible.)

            return Result.Succeeded;
        }

        // ================================================================
        // INPUT POINT DEDUPLICATION
        // Keeps first occurrence of each spatial cluster within tolerance.
        // ================================================================
        private static List<XYZ> DeduplicatePoints(List<XYZ> points, double tolFt)
        {
            List<XYZ> unique = new();

            foreach (XYZ candidate in points)
            {
                bool isDuplicate = unique.Any(u => u.DistanceTo(candidate) <= tolFt);
                if (!isDuplicate)
                    unique.Add(candidate);
            }

            return unique;
        }
    }
}
