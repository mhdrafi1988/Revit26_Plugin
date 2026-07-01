using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTag_V007_A.Helpers;
using Revit26_Plugin.Shared.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V007_A
{
    [Transaction(TransactionMode.Manual)]
    public class RoofTagCommand : IExternalCommand
    {
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
                TaskDialog.Show("Roof Tag V007.A (FaceRef)", "No roof selected.");
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
                TaskDialog.Show("Roof Tag V007.A (FaceRef)", "No valid points found.");
                return Result.Cancelled;
            }

            // ── 5. Deduplicate input points at 10 mm ─────────────────────
            List<XYZ> points = DeduplicatePoints(rawPoints, PointDedupTolFt);

            vm.AddLog(new LogEntry(LogLevel.Info,
                $"Roof id {roof.Id} — {rawPoints.Count} raw pts → {points.Count} unique"));

            // ── 6. Place tags ─────────────────────────────────────────────
            using (Transaction tx = new Transaction(doc, "Place Roof Tags V007.A (FaceRef)"))
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
