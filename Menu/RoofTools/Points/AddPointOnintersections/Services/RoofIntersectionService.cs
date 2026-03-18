using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AddPointOnintersections.Helpers;
using Revit26_Plugin.AddPointOnintersections.Models;

namespace Revit26_Plugin.AddPointOnintersections.Services
{
    public class RoofIntersectionService
    {
        private const double PointTolerance = 1e-6;

        public AddPointsExecutionResult Execute(RoofSelectionContext context, Action<string> log)
        {
            UIDocument uiDoc = context.UiDocument;
            Document doc = uiDoc.Document;

            FootPrintRoof roof = doc.GetElement(context.RoofId) as FootPrintRoof;
            if (roof == null)
            {
                throw new InvalidOperationException("The selected roof is no longer available or is not a FootPrintRoof.");
            }

            List<DetailLine> detailLines = context.DetailLineIds
                .Select(id => doc.GetElement(id))
                .OfType<DetailLine>()
                .ToList();

            if (detailLines.Count == 0)
            {
                throw new InvalidOperationException("No valid detail lines are available.");
            }

            if (roof.Pinned)
            {
                throw new InvalidOperationException("The selected roof is pinned. Unpin the roof and run the command again.");
            }

            SlabShapeEditor shapeEditor = roof.GetSlabShapeEditor();
            if (shapeEditor == null)
            {
                throw new InvalidOperationException("This roof does not provide a valid SlabShapeEditor.");
            }

            using (Transaction tx01 = new Transaction(doc, "Transaction 01 - Enable roof shape editing"))
            {
                tx01.Start();

                shapeEditor.Enable();

                tx01.Commit();
            }

            log?.Invoke("Transaction 01 committed: shape editing enabled.");

            log?.Invoke("Starting Transaction 02: find intersections and add shape points.");

            List<Curve> boundaryCurves = GetAllRoofBoundaryCurves(roof, log);
            if (boundaryCurves.Count == 0)
            {
                throw new InvalidOperationException("No roof boundary curves were found.");
            }

            log?.Invoke($"Roof boundary curves collected: {boundaryCurves.Count}.");

            List<XYZ> uniqueIntersectionPoints = CollectUniqueIntersectionPoints(detailLines, boundaryCurves, log);
            log?.Invoke($"Valid unique intersection points found: {uniqueIntersectionPoints.Count}.");

            int addedCount = 0;
            bool zeroElevationConfirmed = true;

            using (Transaction tx02 = new Transaction(doc, "Transaction 02 - Add roof shape points at intersections"))
            {
                tx02.Start();

                foreach (XYZ point in uniqueIntersectionPoints)
                {
                    try
                    {
                        SlabShapeVertex vertex = shapeEditor.AddPoint(point);

                        if (vertex == null)
                        {
                            log?.Invoke($"Skipped point (not created by Revit): {CurveProjectionHelper.ToReadable(point)}");
                            continue;
                        }

                        shapeEditor.ModifySubElement(vertex, 0.0);
                        addedCount++;

                        log?.Invoke($"Point added at {CurveProjectionHelper.ToReadable(point)} with elevation delta 0.0.");
                    }
                    catch (Exception ex)
                    {
                        zeroElevationConfirmed = false;
                        log?.Invoke($"Failed to add point at {CurveProjectionHelper.ToReadable(point)}. Reason: {ex.Message}");
                    }
                }

                tx02.Commit();
            }

            log?.Invoke("Transaction 02 committed.");

            return new AddPointsExecutionResult
            {
                AddedPointsCount = addedCount,
                ZeroElevationConfirmed = zeroElevationConfirmed
            };
        }

        private static List<Curve> GetAllRoofBoundaryCurves(FootPrintRoof roof, Action<string> log)
        {
            List<Curve> curves = new List<Curve>();

            ModelCurveArrArray profileArray = roof.GetProfiles();
            if (profileArray == null || profileArray.Size == 0)
            {
                return curves;
            }

            int loopIndex = 0;

            foreach (ModelCurveArray modelCurveArray in profileArray)
            {
                loopIndex++;
                int loopCurveCount = 0;

                foreach (ModelCurve modelCurve in modelCurveArray)
                {
                    Curve curve = modelCurve?.GeometryCurve;
                    if (curve == null)
                    {
                        continue;
                    }

                    curves.Add(curve);
                    loopCurveCount++;
                }

                log?.Invoke($"Boundary loop {loopIndex} collected with {loopCurveCount} curves.");
            }

            return curves;
        }

        private static List<XYZ> CollectUniqueIntersectionPoints(
            List<DetailLine> detailLines,
            List<Curve> boundaryCurves,
            Action<string> log)
        {
            List<XYZ> result = new List<XYZ>();

            foreach (DetailLine detailLine in detailLines)
            {
                Curve detailCurve = detailLine.GeometryCurve;
                if (detailCurve == null)
                {
                    log?.Invoke($"Skipped detail line {detailLine.Id.Value}: no geometry curve.");
                    continue;
                }

                Curve flattenedDetailCurve;
                try
                {
                    flattenedDetailCurve = CurveProjectionHelper.CreateFlattenedCurve(detailCurve);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Skipped detail line {detailLine.Id.Value}: {ex.Message}");
                    continue;
                }

                int lineHitCount = 0;

                foreach (Curve boundaryCurve in boundaryCurves)
                {
                    try
                    {
                        Curve flattenedBoundaryCurve = CurveProjectionHelper.CreateFlattenedCurve(boundaryCurve);

                        SetComparisonResult comparison = flattenedBoundaryCurve.Intersect(
                            flattenedDetailCurve,
                            out IntersectionResultArray intersectionResults);

                        if (comparison == SetComparisonResult.Disjoint || intersectionResults == null || intersectionResults.Size == 0)
                        {
                            continue;
                        }

                        for (int i = 0; i < intersectionResults.Size; i++)
                        {
                            IntersectionResult ir = intersectionResults.get_Item(i);
                            if (ir == null || ir.UVPoint == null)
                            {
                                continue;
                            }

                            double boundaryParameter = ir.UVPoint.U;
                            XYZ original3dPoint = boundaryCurve.Evaluate(boundaryParameter, false);

                            if (ContainsPoint(result, original3dPoint))
                            {
                                continue;
                            }

                            result.Add(original3dPoint);
                            lineHitCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"Boundary intersection skipped for detail line {detailLine.Id.Value}: {ex.Message}");
                    }
                }

                log?.Invoke($"Detail line {detailLine.Id.Value}: intersections accepted = {lineHitCount}.");
            }

            return result;
        }

        private static bool ContainsPoint(IEnumerable<XYZ> points, XYZ candidate)
        {
            return points.Any(p =>
                Math.Abs(p.X - candidate.X) <= PointTolerance &&
                Math.Abs(p.Y - candidate.Y) <= PointTolerance &&
                Math.Abs(p.Z - candidate.Z) <= PointTolerance);
        }
    }
}