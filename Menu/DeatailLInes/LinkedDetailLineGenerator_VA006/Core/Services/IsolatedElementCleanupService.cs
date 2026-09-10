using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA006.Core.Engine;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA006.Core.Services
{
    /// <summary>
    /// Section 5 "Remove Isolated Lines &amp; Points" cleanup, run once at the end of
    /// a "Create Detail Lines" run, after every mapping has been processed and
    /// before the transaction commits. Only ever looks at and deletes elements this
    /// same run just created (via <see cref="CreatedElementGroup"/> lists handed
    /// back from each engine) -- it never touches pre-existing Detail Lines already
    /// in the project.
    ///
    /// Definitions:
    ///   - A non-marker Detail Line (Profile loop segment or Linear segment) is
    ///     "isolated" if neither of its endpoints lies within tolerance of any
    ///     endpoint belonging to a DIFFERENT created line. Loop/chain segments
    ///     naturally survive this check because they touch their own neighbors;
    ///     only truly lone segments are removed.
    ///   - A Point-group marker (all curves in one CreatedElementGroup with
    ///     IsPointMarker = true) is "isolated" if none of its curve endpoints lies
    ///     within tolerance of any endpoint belonging to a surviving (non-deleted)
    ///     non-marker line. Isolated markers are removed as a whole unit.
    /// </summary>
    public class IsolatedElementCleanupService
    {
        public CleanupResult RemoveIsolated(
            Document hostDoc,
            IEnumerable<CreatedElementGroup> allGroups,
            double toleranceFeet,
            Action<string>? onLog = null)
        {
            var result = new CleanupResult();
            var groups = allGroups.ToList();

            var lineGroups = groups.Where(g => !g.IsPointMarker).ToList();
            var markerGroups = groups.Where(g => g.IsPointMarker).ToList();

            // id -> (p0, p1) for every non-marker created curve still in the model
            var lineEndpoints = new Dictionary<long, (XYZ p0, XYZ p1)>();
            foreach (var g in lineGroups)
            {
                foreach (var id in g.ElementIds)
                {
                    if (hostDoc.GetElement(new ElementId(id)) is DetailCurve dc && dc.GeometryCurve != null)
                    {
                        try
                        {
                            lineEndpoints[id] = (dc.GeometryCurve.GetEndPoint(0), dc.GeometryCurve.GetEndPoint(1));
                        }
                        catch
                        {
                            // Non-bound curve (e.g. full circle/ellipse) -- never treated
                            // as isolated since it has no meaningful endpoints to compare.
                        }
                    }
                }
            }

            bool Touches(XYZ p, IEnumerable<XYZ> others) => others.Any(o => p.DistanceTo(o) <= toleranceFeet);

            var isolatedLineIds = new List<long>();
            foreach (var kv in lineEndpoints)
            {
                var mine = new[] { kv.Value.p0, kv.Value.p1 };
                bool connected = lineEndpoints
                    .Where(other => other.Key != kv.Key)
                    .Any(other => mine.Any(p => Touches(p, new[] { other.Value.p0, other.Value.p1 })));

                if (!connected)
                    isolatedLineIds.Add(kv.Key);
            }

            foreach (var id in isolatedLineIds)
            {
                try
                {
                    hostDoc.Delete(new ElementId(id));
                    result.LinesRemoved++;
                }
                catch (Exception ex)
                {
                    onLog?.Invoke($"Cleanup: failed to delete isolated line {id}: {ex.Message}");
                }
            }

            // Surviving (non-deleted) line endpoints, for the marker touch-check.
            var survivingPts = lineEndpoints
                .Where(kv => !isolatedLineIds.Contains(kv.Key))
                .SelectMany(kv => new[] { kv.Value.p0, kv.Value.p1 })
                .ToList();

            foreach (var mg in markerGroups)
            {
                var markerPts = new List<XYZ>();
                foreach (var id in mg.ElementIds)
                {
                    if (hostDoc.GetElement(new ElementId(id)) is DetailCurve dc && dc.GeometryCurve != null)
                    {
                        try
                        {
                            markerPts.Add(dc.GeometryCurve.GetEndPoint(0));
                            markerPts.Add(dc.GeometryCurve.GetEndPoint(1));
                        }
                        catch { /* unbound curve -- ignore for touch-check */ }
                    }
                }

                bool touchesSurvivingLine = markerPts.Any(p => Touches(p, survivingPts));
                if (touchesSurvivingLine) continue;

                foreach (var id in mg.ElementIds)
                {
                    try
                    {
                        hostDoc.Delete(new ElementId(id));
                        result.MarkerCurvesRemoved++;
                    }
                    catch (Exception ex)
                    {
                        onLog?.Invoke($"Cleanup: failed to delete isolated marker element {id}: {ex.Message}");
                    }
                }
                result.MarkersRemoved++;
            }

            return result;
        }
    }

    public class CleanupResult
    {
        public int LinesRemoved { get; set; }
        public int MarkersRemoved { get; set; }
        public int MarkerCurvesRemoved { get; set; }
    }
}
