using Autodesk.Revit.DB;
using Revit26_Plugin.PerpendicularPointoDrain.V01.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Services
{
    /// <summary>
    /// For each drain-group centroid, searches every boundary loop (outer + interior, per
    /// caller's selection) in up to 8 compass directions (Project North = +Y) and computes the
    /// perpendicular projection of the centroid onto the nearest valid boundary segment in each
    /// direction. If a direction has no exact-sector hit, falls back to the nearest valid edge
    /// on that loop regardless of direction (per the agreed design). A "valid" hit requires the
    /// true perpendicular foot to land strictly within the finite segment — not clamped to an
    /// endpoint — matching the validation rule from the spec.
    ///
    /// Split into ComputeCandidates (dry run, no model changes) and ApplyPoints (commits via
    /// SlabShapeEditor inside a caller-managed transaction), mirroring the Analyze/Apply split
    /// already used in RoofLoopAnalyzer.
    /// </summary>
    public class BoundaryProjectionService
    {
        private static readonly string[] DirectionOrder = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };

        // ─────────────────────────────────────────────────────────────────────────
        // ANALYZE (dry run)
        // ─────────────────────────────────────────────────────────────────────────
        public List<ProjectionResultModel> ComputeCandidates(
            DrainGroupModel group,
            List<LoopBoundaryModel> loops,
            List<string> enabledDirections)
        {
            var results    = new List<ProjectionResultModel>();
            var enabledSet = new HashSet<string>(enabledDirections);

            foreach (var loop in loops)
            {
                var candidates = new List<(XYZ foot, double dist, double angleDeg)>();

                foreach (var curve in loop.Curves)
                {
                    if (TryGetPerpendicularFoot(curve, group.Centroid, out XYZ foot, out double dist))
                    {
                        double angleDeg = NormalizeDegrees(
                            Math.Atan2(foot.Y - group.Centroid.Y, foot.X - group.Centroid.X) * 180.0 / Math.PI);
                        candidates.Add((foot, dist, angleDeg));
                    }
                }

                if (!candidates.Any())
                {
                    results.Add(new ProjectionResultModel
                    {
                        GroupLabel = group.Label,
                        Direction  = "-",
                        LoopLabel  = loop.Label,
                        Status     = "Warning - no valid perpendicular foot on this loop"
                    });
                    continue;
                }

                var bySector = candidates
                    .GroupBy(c => NearestSectorIndex(c.angleDeg))
                    .ToDictionary(g => g.Key, g => g.OrderBy(c => c.dist).First());

                var globalNearest = candidates.OrderBy(c => c.dist).First();

                for (int i = 0; i < DirectionOrder.Length; i++)
                {
                    string dirName = DirectionOrder[i];
                    if (!enabledSet.Contains(dirName)) continue;

                    bool hasSectorHit = bySector.TryGetValue(i, out var hit);
                    var chosen = hasSectorHit ? hit : globalNearest;

                    results.Add(new ProjectionResultModel
                    {
                        GroupLabel = group.Label,
                        Direction  = dirName,
                        LoopLabel  = loop.Label,
                        DistanceMm = Math.Round(UnitUtils.ConvertFromInternalUnits(chosen.dist, UnitTypeId.Millimeters), 1),
                        Point      = chosen.foot,
                        IsFallback = !hasSectorHit,
                        Status     = "Pending"
                    });
                }
            }

            return results;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // APPLY (commit) — caller starts/commits the transaction
        // ─────────────────────────────────────────────────────────────────────────
        public void ApplyPoints(SlabShapeEditor editor, IEnumerable<ProjectionResultModel> results,
                                 bool snapToExistingVertex, double snapToleranceFeet)
        {
            foreach (var r in results)
            {
                if (r.Point == null) continue; // warning rows — nothing to apply

                if (snapToExistingVertex && HasNearbyVertex(editor, r.Point, snapToleranceFeet))
                {
                    r.Status = "Skipped (existing vertex)";
                    continue;
                }

                try
                {
                    editor.AddPoint(r.Point);
                    r.Status = r.IsFallback ? "Added (fallback)" : "Added";
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    r.Status = "Skipped (duplicate)";
                }
            }
        }

        private bool HasNearbyVertex(SlabShapeEditor editor, XYZ pt, double toleranceFeet)
        {
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
            {
                double dx = v.Position.X - pt.X;
                double dy = v.Position.Y - pt.Y;
                if (Math.Sqrt(dx * dx + dy * dy) <= toleranceFeet)
                    return true;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Perpendicular foot — valid only if it lands strictly within the segment.
        // Uses Curve.Project(), which already clamps to the curve's bounded range —
        // if the true perpendicular foot would fall outside the segment, the result's
        // parameter sits essentially at one of the endpoints. Works generically for
        // Lines, Arcs, or any other bounded curve type, with no manual angle math.
        // ─────────────────────────────────────────────────────────────────────────
        private bool TryGetPerpendicularFoot(Curve curve, XYZ point, out XYZ foot, out double dist)
        {
            foot = null;
            dist = 0;

            if (curve == null || !curve.IsBound) return false;

            IntersectionResult result;
            try
            {
                result = curve.Project(point);
            }
            catch
            {
                return false;
            }

            if (result == null) return false;

            double param      = result.Parameter;
            double startParam = curve.GetEndParameter(0);
            double endParam   = curve.GetEndParameter(1);
            double eps        = Math.Max(Math.Abs(endParam - startParam) * 1e-4, 1e-6);

            // Clamped to (or essentially at) an endpoint means the true unclamped foot
            // falls outside this segment — not a genuine projection onto it.
            if (param <= startParam + eps || param >= endParam - eps)
                return false;

            foot = result.XYZPoint;
            dist = Math.Sqrt(Math.Pow(point.X - foot.X, 2) + Math.Pow(point.Y - foot.Y, 2));
            return true;
        }

        private int NearestSectorIndex(double angleDeg)
        {
            int idx = (int)Math.Round(angleDeg / 45.0) % 8;
            if (idx < 0) idx += 8;
            return idx;
        }

        private double NormalizeDegrees(double deg)
        {
            deg %= 360;
            if (deg < 0) deg += 360;
            return deg;
        }
    }
}
