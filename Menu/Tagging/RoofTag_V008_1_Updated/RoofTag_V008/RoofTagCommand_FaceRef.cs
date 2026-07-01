using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTag_V008.Helpers;
using Revit26_Plugin.Shared.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V008
{
    /// <summary>
    /// V008: Roof Tag Command with FaceRef method.
    /// Uses user-configurable clustering tolerance (replaces hardcoded 10mm/500mm).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RoofTagCommand : IExternalCommand
    {
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
                TaskDialog.Show("Roof Tag V008 (FaceRef)", "No roof selected.");
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
                TaskDialog.Show("Roof Tag V008 (FaceRef)", "No valid points found.");
                return Result.Cancelled;
            }

            // ── 4b. Filter by minimum point distance (if enabled) ──────────
            List<XYZ> filteredPoints = rawPoints;
            int filteredCount = 0;

            if (vm.EnableMinimumPointDistance && vm.MinimumPointDistance > 0)
            {
                double minDistFt = UnitUtils.ConvertToInternalUnits(vm.MinimumPointDistance, UnitTypeId.Millimeters);
                filteredPoints = FilterByMinimumDistance(rawPoints, minDistFt);
                filteredCount = rawPoints.Count - filteredPoints.Count;
                
                vm.AddLog(new LogEntry(LogLevel.Info,
                    $"Min distance filter: removed {filteredCount} very close pts (threshold: {vm.MinimumPointDistance:F1} mm)"));
            }

            // ── 5. Set total count and deduplicate using user-configured tolerance
            vm.TotalCount = filteredPoints.Count;

            // Convert tolerance from mm to feet for deduplication
            double tolFt = UnitUtils.ConvertToInternalUnits(vm.ClusteringTolerance, UnitTypeId.Millimeters);
            List<XYZ> points = DeduplicatePoints(filteredPoints, tolFt);

            int removedCount = filteredPoints.Count - points.Count;
            vm.RemovedCount = removedCount;

            vm.AddLog(new LogEntry(LogLevel.Info,
                $"Roof id {roof.Id} — {filteredPoints.Count} pts → {points.Count} unique (removed {removedCount})"));

            // ── 6. Place tags ─────────────────────────────────────────────
            using (Transaction tx = new Transaction(doc, "Place Roof Tags V008 (FaceRef)"))
            {
                tx.Start();

                foreach (XYZ pt in points)
                {
                    RoofTagGeometryHelper.GetTaggingReferenceOnRoof(
                        roof, pt,
                        out Reference faceRef,
                        out XYZ projected);

                    XYZ origin = projected ?? pt;

                    LogEntry result = RoofTaggingService_FaceRef.PlaceTag(doc, faceRef, roof, origin, vm);
                    vm.AddLog(result);
                }

                tx.Commit();
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Deduplicate points using the specified tolerance in feet.
        /// Points within tolFt distance of an existing point are considered duplicates.
        /// </summary>
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

        /// <summary>
        /// Filter points by minimum distance.
        /// Removes points that are closer than minDistFt to any previously kept point.
        /// Processes points in order; first point is always kept.
        /// </summary>
        private static List<XYZ> FilterByMinimumDistance(List<XYZ> points, double minDistFt)
        {
            if (points == null || points.Count == 0)
                return new List<XYZ>();

            List<XYZ> filtered = new() { points[0] };  // Always keep first point

            foreach (XYZ candidate in points.Skip(1))
            {
                bool isTooClose = filtered.Any(f => f.DistanceTo(candidate) < minDistFt);
                if (!isTooClose)
                    filtered.Add(candidate);
            }

            return filtered;
        }
    }
}
