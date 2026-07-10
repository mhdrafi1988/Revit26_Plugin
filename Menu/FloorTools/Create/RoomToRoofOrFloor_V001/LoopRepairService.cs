using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoomToRoofOrFloor.V001.Core.Services
{
    /// <summary>
    /// Repairs a room's raw boundary loop(s) ONCE before either roof or
    /// floor placement is attempted. The repaired loops are cached by the
    /// caller (RoomToRoofOrFloorEngine) and reused for both attempts.
    ///
    /// Fixed repair order (per convention, do not reorder):
    ///   1. Dedupe near-duplicate points
    ///   2. Resolve self-intersections
    ///   3. Close open loops
    ///   4. Resolve inner/outer loop conflicts
    ///
    /// Best-effort: if a loop cannot be repaired, IsRepairable is false
    /// and the caller must skip the room entirely (never place from a
    /// loop that could not be made valid).
    /// </summary>
    public class LoopRepairService
    {
        private const double PointMergeTolerance = 0.01; // feet, ~3mm
        private const double GapCloseTolerance = 0.05;    // feet, ~15mm

        public LoopRepairResult Repair(IList<CurveLoop> rawLoops)
        {
            var notes = new List<string>();

            if (rawLoops == null || rawLoops.Count == 0)
                return LoopRepairResult.Failure("No boundary loops found");

            var working = rawLoops.ToList();

            // STEP 1: dedupe near-duplicate points per loop
            working = working.Select(l => DedupePoints(l, notes)).ToList();

            // STEP 2: resolve self-intersections per loop (best-effort flag)
            var afterIntersection = new List<CurveLoop>();
            foreach (var loop in working)
            {
                var fixedLoop = TryResolveSelfIntersections(loop, notes);
                if (fixedLoop == null)
                    return LoopRepairResult.Failure("Self-intersection could not be resolved");
                afterIntersection.Add(fixedLoop);
            }
            working = afterIntersection;

            // STEP 3: close open loops
            var afterClose = new List<CurveLoop>();
            foreach (var loop in working)
            {
                var closed = TryCloseLoop(loop, notes);
                if (closed == null)
                    return LoopRepairResult.Failure("Open loop gap too large to close automatically");
                afterClose.Add(closed);
            }
            working = afterClose;

            // STEP 4: resolve inner/outer conflicts (multiple loops)
            working = ResolveInnerOuterConflicts(working, notes);

            return LoopRepairResult.Success(working, string.Join("; ", notes));
        }

        private CurveLoop DedupePoints(CurveLoop loop, List<string> notes)
        {
            var curves = loop.ToList();
            if (curves.Count == 0) return loop;

            var cleaned = new List<Curve>();
            XYZ lastEnd = null;
            int removed = 0;

            foreach (var curve in curves)
            {
                var start = curve.GetEndPoint(0);
                if (lastEnd != null && start.DistanceTo(lastEnd) < PointMergeTolerance)
                {
                    removed++;
                    start = lastEnd;
                }

                var end = curve.GetEndPoint(1);

                // Only lines get their endpoints snapped/rebuilt here — arcs
                // are left untouched to avoid corrupting their curvature
                // (mirrors the accepted arc-handling caution from
                // AutoSlopeByPoint's DijkstraPathEngine).
                Curve rebuilt = curve is Line ? Line.CreateBound(start, end) : curve;

                cleaned.Add(rebuilt);
                lastEnd = end;
            }

            if (removed > 0)
                notes.Add($"Deduped {removed} near-duplicate point(s)");

            var result = new CurveLoop();
            foreach (var c in cleaned) result.Append(c);
            return result;
        }

        private CurveLoop TryResolveSelfIntersections(CurveLoop loop, List<string> notes)
        {
            var curves = loop.ToList();
            bool foundIntersection = false;

            for (int i = 0; i < curves.Count; i++)
            {
                for (int j = i + 2; j < curves.Count; j++)
                {
                    if (i == 0 && j == curves.Count - 1) continue; // adjacent, shares endpoint by design

                    var comparison = curves[i].Intersect(curves[j], out var results);
                    if (comparison == SetComparisonResult.Overlap && results != null && results.Size > 0)
                        foundIntersection = true;
                }
            }

            if (!foundIntersection)
                return loop;

            // NOTE (flag to Rafi): this is a best-effort detector only — it
            // does not geometrically re-route crossing segments. If Revit
            // still rejects the loop downstream during placement, the room
            // is skipped and logged (never placed with bad geometry).
            notes.Add("Self-intersection detected (best-effort flag, not re-routed)");
            return loop;
        }

        private CurveLoop TryCloseLoop(CurveLoop loop, List<string> notes)
        {
            var curves = loop.ToList();
            if (curves.Count == 0) return null;

            var first = curves.First().GetEndPoint(0);
            var last = curves.Last().GetEndPoint(1);
            var gap = first.DistanceTo(last);

            if (gap < 1e-6)
                return loop; // already closed

            if (gap > GapCloseTolerance)
            {
                notes.Add($"Open loop gap too large ({gap:F3} ft) to close automatically");
                return null;
            }

            var result = new CurveLoop();
            foreach (var c in curves) result.Append(c);
            result.Append(Line.CreateBound(last, first));

            notes.Add($"Closed open loop (gap {gap:F3} ft)");
            return result;
        }

        private List<CurveLoop> ResolveInnerOuterConflicts(List<CurveLoop> loops, List<string> notes)
        {
            if (loops.Count <= 1)
                return loops;

            // Largest-area loop is the outer boundary; remaining loops are
            // inner (islands) and must wind opposite the outer loop.
            var ordered = loops
                .Select(l => new { Loop = l, Area = Math.Abs(SignedArea(l)) })
                .OrderByDescending(x => x.Area)
                .ToList();

            var outer = ordered.First().Loop;
            var outerClockwise = SignedArea(outer) < 0;
            var result = new List<CurveLoop> { outer };

            for (int i = 1; i < ordered.Count; i++)
            {
                var inner = ordered[i].Loop;
                var innerClockwise = SignedArea(inner) < 0;

                if (innerClockwise == outerClockwise)
                {
                    inner = ReverseLoop(inner);
                    notes.Add("Flipped inner loop orientation to resolve inner/outer conflict");
                }

                result.Add(inner);
            }

            return result;
        }

        private CurveLoop ReverseLoop(CurveLoop loop)
        {
            var curves = loop.ToList();
            curves.Reverse();
            var result = new CurveLoop();
            foreach (var c in curves)
                result.Append(c.CreateReversed());
            return result;
        }

        private double SignedArea(CurveLoop loop)
        {
            // Shoelace formula on tessellated points, projected to XY.
            var points = new List<XYZ>();
            foreach (var curve in loop)
                points.AddRange(curve.Tessellate());

            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                area += (p1.X * p2.Y) - (p2.X * p1.Y);
            }
            return area / 2.0;
        }
    }

    /// <summary>Result of a repair attempt: either repaired loops + notes, or a failure reason.</summary>
    public class LoopRepairResult
    {
        public bool IsRepairable { get; }
        public IList<CurveLoop> RepairedLoops { get; }
        public string Notes { get; }
        public string FailureReason { get; }

        private LoopRepairResult(bool ok, IList<CurveLoop> loops, string notes, string failureReason)
        {
            IsRepairable = ok;
            RepairedLoops = loops;
            Notes = notes;
            FailureReason = failureReason;
        }

        public static LoopRepairResult Success(IList<CurveLoop> loops, string notes) => new(true, loops, notes, null);
        public static LoopRepairResult Failure(string reason) => new(false, null, null, reason);
    }
}
