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
        private const double XyTolerance = 1e-6;

        // Exact add is attempted first.
        // If Revit rejects a boundary point, try a very small move along the selected detail line
        // on both sides to land on the editable top face.
        private static readonly double[] BoundaryFallbackOffsetsFeet =
        {
            1e-4, 5e-4, 1e-3
        };

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

            List<IntersectionCandidate> candidates = CollectIntersectionCandidates(detailLines, boundaryCurves, log);
            log?.Invoke($"Unique intersection points found: {candidates.Count}.");

            int addedCount = 0;
            bool zeroElevationConfirmed = true;

            using (Transaction tx02 = new Transaction(doc, "Transaction 02 - Add roof shape points at intersections"))
            {
                tx02.Start();

                foreach (IntersectionCandidate candidate in candidates)
                {
                    try
                    {
                        SlabShapeVertex vertex = TryAddPoint(shapeEditor, candidate, log);

                        if (vertex == null)
                        {
                            log?.Invoke(
                                $"Skipped intersection. Revit did not create a shape point for " +
                                $"{CurveProjectionHelper.ToReadable(candidate.ExactPoint)}.");
                            continue;
                        }

                        shapeEditor.ModifySubElement(vertex, 0.0);
                        addedCount++;

                        log?.Invoke(
                            $"Point added. Exact intersection: {CurveProjectionHelper.ToReadable(candidate.ExactPoint)} | " +
                            $"Created point: {CurveProjectionHelper.ToReadable(candidate.CreatedPoint)} | " +
                            $"Detail line: {candidate.DetailLineId.Value}.");
                    }
                    catch (Exception ex)
                    {
                        zeroElevationConfirmed = false;
                        log?.Invoke(
                            $"Failed to add point at {CurveProjectionHelper.ToReadable(candidate.ExactPoint)}. " +
                            $"Reason: {ex.Message}");
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

        private static List<IntersectionCandidate> CollectIntersectionCandidates(
            List<DetailLine> detailLines,
            List<Curve> boundaryCurves,
            Action<string> log)
        {
            List<IntersectionCandidate> allCandidates = new List<IntersectionCandidate>();

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

                XYZ planDirection;
                try
                {
                    planDirection = GetPlanDirection(flattenedDetailCurve);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Skipped detail line {detailLine.Id.Value}: {ex.Message}");
                    continue;
                }

                List<IntersectionCandidate> lineCandidates = new List<IntersectionCandidate>();

                foreach (Curve boundaryCurve in boundaryCurves)
                {
                    Curve flattenedBoundaryCurve;
                    try
                    {
                        flattenedBoundaryCurve = CurveProjectionHelper.CreateFlattenedCurve(boundaryCurve);
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"Skipped one boundary curve during flattening: {ex.Message}");
                        continue;
                    }

                    try
                    {
                        SetComparisonResult comparison = flattenedBoundaryCurve.Intersect(
                            flattenedDetailCurve,
                            out IntersectionResultArray intersectionResults);

                        if (comparison == SetComparisonResult.Disjoint || intersectionResults == null || intersectionResults.Size == 0)
                        {
                            continue;
                        }

                        for (int i = 0; i < intersectionResults.Size; i++)
                        {
                            IntersectionResult result = intersectionResults.get_Item(i);
                            if (!TryCreateCandidate(
                                    detailLine.Id,
                                    detailCurve,
                                    flattenedDetailCurve,
                                    boundaryCurve,
                                    result,
                                    planDirection,
                                    out IntersectionCandidate candidate,
                                    log))
                            {
                                continue;
                            }

                            if (ContainsCandidate(lineCandidates, candidate.ExactPoint))
                            {
                                continue;
                            }

                            lineCandidates.Add(candidate);
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"Boundary intersection skipped for detail line {detailLine.Id.Value}: {ex.Message}");
                    }
                }

                lineCandidates = lineCandidates
                    .OrderBy(x => x.DetailParameter)
                    .ToList();

                foreach (IntersectionCandidate lineCandidate in lineCandidates)
                {
                    if (ContainsCandidate(allCandidates, lineCandidate.ExactPoint))
                    {
                        continue;
                    }

                    allCandidates.Add(lineCandidate);
                }

                log?.Invoke($"Detail line {detailLine.Id.Value}: intersections accepted = {lineCandidates.Count}.");
            }

            return allCandidates;
        }

        private static bool TryCreateCandidate(
            ElementId detailLineId,
            Curve originalDetailCurve,
            Curve flattenedDetailCurve,
            Curve originalBoundaryCurve,
            IntersectionResult result,
            XYZ planDirection,
            out IntersectionCandidate candidate,
            Action<string> log)
        {
            candidate = null;

            if (result?.XYZPoint == null || result.UVPoint == null)
            {
                return false;
            }

            XYZ flatIntersectionPoint = result.XYZPoint;

            // U belongs to the first curve passed to Intersect() => boundary curve.
            double boundaryParameter = result.UVPoint.U;

            // V belongs to the second curve passed to Intersect() => detail curve.
            double detailParameter = result.UVPoint.V;

            XYZ rebuiltBoundaryPoint;
            try
            {
                rebuiltBoundaryPoint = originalBoundaryCurve.Evaluate(boundaryParameter, false);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Rejected one intersection: failed to evaluate boundary curve. Reason: {ex.Message}");
                return false;
            }

            XYZ rebuiltFlatPoint = new XYZ(rebuiltBoundaryPoint.X, rebuiltBoundaryPoint.Y, 0.0);
            XYZ exactFlatPoint = new XYZ(flatIntersectionPoint.X, flatIntersectionPoint.Y, 0.0);

            if (!CurveProjectionHelper.IsAlmostEqualXY(rebuiltFlatPoint, exactFlatPoint, XyTolerance))
            {
                log?.Invoke(
                    $"Rejected one intersection due to XY mismatch. " +
                    $"Expected {CurveProjectionHelper.ToReadable(exactFlatPoint)}, " +
                    $"rebuilt {CurveProjectionHelper.ToReadable(rebuiltFlatPoint)}.");
                return false;
            }

            XYZ exactPoint = rebuiltBoundaryPoint;

            // Validate that the point still lies on the selected detail line in plan.
            if (!IsPointOnFlattenedCurve(flattenedDetailCurve, exactFlatPoint))
            {
                log?.Invoke(
                    $"Rejected one intersection because it does not lie on the flattened detail line: " +
                    $"{CurveProjectionHelper.ToReadable(exactPoint)}.");
                return false;
            }

            candidate = new IntersectionCandidate(
                detailLineId,
                exactPoint,
                detailParameter,
                planDirection);

            return true;
        }

        private static SlabShapeVertex TryAddPoint(
            SlabShapeEditor shapeEditor,
            IntersectionCandidate candidate,
            Action<string> log)
        {
            // 1) Exact point first.
            SlabShapeVertex vertex = shapeEditor.AddPoint(candidate.ExactPoint);
            if (vertex != null)
            {
                candidate.CreatedPoint = candidate.ExactPoint;
                return vertex;
            }

            log?.Invoke(
                $"Exact point was rejected by Revit at {CurveProjectionHelper.ToReadable(candidate.ExactPoint)}. " +
                $"Trying boundary fallback on both sides of the detail line.");

            // 2) Revit often rejects boundary points. Try both directions along the detail line.
            foreach (double offset in BoundaryFallbackOffsetsFeet)
            {
                XYZ plus = candidate.ExactPoint + (candidate.DetailPlanDirection * offset);
                vertex = shapeEditor.AddPoint(plus);
                if (vertex != null)
                {
                    candidate.CreatedPoint = plus;
                    log?.Invoke(
                        $"Fallback succeeded at +{offset:0.######} ft from the exact intersection: " +
                        $"{CurveProjectionHelper.ToReadable(plus)}.");
                    return vertex;
                }

                XYZ minus = candidate.ExactPoint - (candidate.DetailPlanDirection * offset);
                vertex = shapeEditor.AddPoint(minus);
                if (vertex != null)
                {
                    candidate.CreatedPoint = minus;
                    log?.Invoke(
                        $"Fallback succeeded at -{offset:0.######} ft from the exact intersection: " +
                        $"{CurveProjectionHelper.ToReadable(minus)}.");
                    return vertex;
                }
            }

            return null;
        }

        private static bool ContainsCandidate(IEnumerable<IntersectionCandidate> candidates, XYZ point)
        {
            return candidates.Any(x => CurveProjectionHelper.IsAlmostEqualXY(x.ExactPoint, point, XyTolerance));
        }

        private static bool IsPointOnFlattenedCurve(Curve flattenedCurve, XYZ flatPoint)
        {
            IntersectionResult projection = flattenedCurve.Project(flatPoint);
            if (projection?.XYZPoint == null)
            {
                return false;
            }

            return CurveProjectionHelper.IsAlmostEqualXY(projection.XYZPoint, flatPoint, XyTolerance);
        }

        private static XYZ GetPlanDirection(Curve flattenedCurve)
        {
            XYZ start = flattenedCurve.GetEndPoint(0);
            XYZ end = flattenedCurve.GetEndPoint(1);
            XYZ vector = new XYZ(end.X - start.X, end.Y - start.Y, 0.0);

            if (vector.GetLength() <= XyTolerance)
            {
                throw new InvalidOperationException("Detail line has zero plan length.");
            }

            return vector.Normalize();
        }

        private sealed class IntersectionCandidate
        {
            public IntersectionCandidate(
                ElementId detailLineId,
                XYZ exactPoint,
                double detailParameter,
                XYZ detailPlanDirection)
            {
                DetailLineId = detailLineId;
                ExactPoint = exactPoint ?? throw new ArgumentNullException(nameof(exactPoint));
                DetailParameter = detailParameter;
                DetailPlanDirection = detailPlanDirection ?? throw new ArgumentNullException(nameof(detailPlanDirection));
                CreatedPoint = exactPoint;
            }

            public ElementId DetailLineId { get; }
            public XYZ ExactPoint { get; }
            public double DetailParameter { get; }
            public XYZ DetailPlanDirection { get; }
            public XYZ CreatedPoint { get; set; }
        }
    }
}