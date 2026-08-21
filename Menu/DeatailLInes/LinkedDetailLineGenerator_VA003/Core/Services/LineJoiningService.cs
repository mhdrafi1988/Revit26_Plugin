using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// VA003 same-source-element line cleanup: given the ordered curve list produced
    /// for ONE element (a clipped Linear centerline's segments, or one Profile loop's
    /// clipped chain), applies up to four independently-toggleable behaviors — never
    /// comparing curves from different elements, per Rafi's explicit "no cross-element
    /// checking" instruction:
    ///
    /// 1. RemoveEngulfedOnly: a shorter straight segment fully contained within a
    ///    longer overlapping segment on the same axis is dropped.
    /// 2. MergePartialOverlaps: two straight segments on the same axis that partially
    ///    overlap (neither fully contains the other) are combined into one new line
    ///    spanning the full combined extent.
    /// 3. JoinCollinearLines: straight segments on the same axis that don't overlap at
    ///    all, but are separated by no more than the tolerance (or share an endpoint
    ///    exactly), are merged into one continuous line.
    /// 4. Intersection trim/extend (gated by JoinCollinearLines): two NON-parallel
    ///    straight segments (from this same element) that fall short of their true
    ///    geometric intersection by no more than the tolerance are extended/trimmed to
    ///    meet there exactly. A pair that already crosses inside both segments' bounded
    ///    spans is left untouched. Never fabricates a closed loop by itself.
    ///
    /// Only Line/Line pairs are candidates for any of the above; Arc/Ellipse/spline
    /// segments are passed through unchanged and never merged into.
    /// </summary>
    public class LineJoiningService
    {
        private const double AngleToleranceRad = 1e-4;

        /// <summary>Applies the enabled cleanup behaviors to one element's own curve
        /// list. toleranceFeet is shared across all checks (perpendicular axis
        /// tolerance, collinear gap tolerance, and intersection extension distance).</summary>
        public List<Curve> ProcessLines(
            List<Curve> curves,
            double toleranceFeet,
            bool removeEngulfedOnly,
            bool mergePartialOverlaps,
            bool joinCollinearLines)
        {
            if (curves.Count < 2 || !(removeEngulfedOnly || mergePartialOverlaps || joinCollinearLines))
                return curves;

            var passthrough = new List<Curve>();
            var lines = new List<Line>();

            foreach (var c in curves)
            {
                if (c is Line line && line.Length > 1e-9)
                    lines.Add(line);
                else
                    passthrough.Add(c);
            }

            if (lines.Count < 2)
                return curves;

            List<Line> mergedLines = MergeByAxis(lines, toleranceFeet, removeEngulfedOnly, mergePartialOverlaps, joinCollinearLines);

            if (joinCollinearLines)
                mergedLines = ExtendAndTrimToIntersections(mergedLines, toleranceFeet);

            var result = new List<Curve>(passthrough);
            result.AddRange(mergedLines);
            return result;
        }

        // -- Same-axis engulfment / partial-overlap / gap-join sweep --------------

        private List<Line> MergeByAxis(
            List<Line> lines, double toleranceFeet,
            bool removeEngulfedOnly, bool mergePartialOverlaps, bool joinCollinearLines)
        {
            var buckets = new List<List<Line>>();
            var axisRepr = new List<(XYZ Origin, XYZ Dir)>();

            foreach (var line in lines)
            {
                XYZ dir = (line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize();

                bool placed = false;
                for (int b = 0; b < buckets.Count; b++)
                {
                    if (IsSameAxis(axisRepr[b].Origin, axisRepr[b].Dir, line.GetEndPoint(0), dir, toleranceFeet))
                    {
                        buckets[b].Add(line);
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    buckets.Add(new List<Line> { line });
                    axisRepr.Add((line.GetEndPoint(0), dir));
                }
            }

            var result = new List<Line>();

            for (int b = 0; b < buckets.Count; b++)
            {
                var bucket = buckets[b];
                if (bucket.Count == 1) { result.Add(bucket[0]); continue; }

                XYZ origin = axisRepr[b].Origin;
                XYZ axisDir = axisRepr[b].Dir;

                var intervals = bucket.Select(line =>
                {
                    double p0 = (line.GetEndPoint(0) - origin).DotProduct(axisDir);
                    double p1 = (line.GetEndPoint(1) - origin).DotProduct(axisDir);
                    return (Min: Math.Min(p0, p1), Max: Math.Max(p0, p1));
                }).OrderBy(iv => iv.Min).ToList();

                var run = intervals[0];
                for (int i = 1; i < intervals.Count; i++)
                {
                    var next = intervals[i];

                    bool overlapping = next.Min < run.Max - 1e-9;
                    bool engulfed = overlapping && next.Max <= run.Max + 1e-9;
                    bool gapJoin = !overlapping && next.Min <= run.Max + toleranceFeet;

                    bool shouldMerge =
                        (engulfed && removeEngulfedOnly) ||
                        (overlapping && !engulfed && mergePartialOverlaps) ||
                        (gapJoin && joinCollinearLines);

                    if (shouldMerge)
                    {
                        run.Max = Math.Max(run.Max, next.Max);
                    }
                    else
                    {
                        result.Add(Line.CreateBound(origin + axisDir * run.Min, origin + axisDir * run.Max));
                        run = next;
                    }
                }
                result.Add(Line.CreateBound(origin + axisDir * run.Min, origin + axisDir * run.Max));
            }

            return result;
        }

        private bool IsSameAxis(XYZ axisOrigin, XYZ axisDir, XYZ testPoint, XYZ testDir, double tol)
        {
            double angle = axisDir.AngleTo(testDir);
            double angleFromAxis = Math.Min(angle, Math.Abs(Math.PI - angle));
            if (angleFromAxis > AngleToleranceRad) return false;

            XYZ toPoint = testPoint - axisOrigin;
            double along = toPoint.DotProduct(axisDir);
            XYZ closest = axisOrigin + axisDir * along;
            return testPoint.DistanceTo(closest) <= tol;
        }

        // -- Same-element intersection trim/extend --------------------------------

        /// <summary>For every pair of non-parallel lines in this element's own
        /// (already axis-merged) curve list, computes the true intersection of their
        /// infinite axes and extends/trims whichever line(s) fall short of (or
        /// overshoot) that point by no more than toleranceFeet, so the pair meets
        /// exactly there. A pair already crossing inside both lines' bounded spans is
        /// left alone. Each line accepts at most one extension per end — the closest
        /// qualifying candidate.</summary>
        private List<Line> ExtendAndTrimToIntersections(List<Line> lines, double toleranceFeet)
        {
            if (lines.Count < 2) return lines;

            var best = new Dictionary<(int idx, int end), (XYZ point, double dist)>();

            void Register(int idx, int end, XYZ point, double dist)
            {
                var key = (idx, end);
                if (!best.TryGetValue(key, out var existing) || dist < existing.dist)
                    best[key] = (point, dist);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    Line a = lines[i];
                    Line b = lines[j];

                    XYZ dirA = (a.GetEndPoint(1) - a.GetEndPoint(0)).Normalize();
                    XYZ dirB = (b.GetEndPoint(1) - b.GetEndPoint(0)).Normalize();

                    double angle = dirA.AngleTo(dirB);
                    double angleFromParallel = Math.Min(angle, Math.Abs(Math.PI - angle));
                    if (angleFromParallel < AngleToleranceRad) continue; // parallel/collinear — handled by axis merge

                    if (!TryIntersectInfiniteLines(a.GetEndPoint(0), dirA, b.GetEndPoint(0), dirB, out XYZ p)) continue;

                    bool onA = ClassifyAgainstSegment(p, a.GetEndPoint(0), a.GetEndPoint(1), out double overshootA, out int nearEndA);
                    bool onB = ClassifyAgainstSegment(p, b.GetEndPoint(0), b.GetEndPoint(1), out double overshootB, out int nearEndB);

                    if (onA && onB) continue; // already crossing — nothing to do

                    if (onA)
                    {
                        if (overshootB <= toleranceFeet) Register(j, nearEndB, p, overshootB);
                    }
                    else if (onB)
                    {
                        if (overshootA <= toleranceFeet) Register(i, nearEndA, p, overshootA);
                    }
                    else if (overshootA <= toleranceFeet && overshootB <= toleranceFeet)
                    {
                        Register(i, nearEndA, p, overshootA);
                        Register(j, nearEndB, p, overshootB);
                    }
                }
            }

            if (best.Count == 0) return lines;

            var updated = new List<Line>(lines);

            foreach (var group in best.GroupBy(kv => kv.Key.idx))
            {
                int idx = group.Key;
                Line entry = updated[idx];
                XYZ newStart = entry.GetEndPoint(0);
                XYZ newEnd = entry.GetEndPoint(1);

                foreach (var kv in group)
                {
                    if (kv.Key.end == 0) newStart = kv.Value.point;
                    else newEnd = kv.Value.point;
                }

                if (newStart.IsAlmostEqualTo(entry.GetEndPoint(0)) && newEnd.IsAlmostEqualTo(entry.GetEndPoint(1)))
                    continue;

                if (newStart.DistanceTo(newEnd) < 1e-9) continue; // degenerate — skip, keep original

                updated[idx] = Line.CreateBound(newStart, newEnd);
            }

            return updated;
        }

        private bool TryIntersectInfiniteLines(XYZ p1, XYZ d1, XYZ p2, XYZ d2, out XYZ intersection)
        {
            double denom = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(denom) < 1e-9)
            {
                intersection = XYZ.Zero;
                return false;
            }
            double t = ((p2.X - p1.X) * d2.Y - (p2.Y - p1.Y) * d2.X) / denom;
            intersection = p1 + d1 * t;
            return true;
        }

        /// <summary>True if p projects within the segment's bounded span (a true
        /// crossing). Otherwise returns the overshoot distance beyond the nearer
        /// endpoint and which endpoint (0=Start, 1=End) that is.</summary>
        private bool ClassifyAgainstSegment(XYZ p, XYZ start, XYZ end, out double overshoot, out int nearEnd)
        {
            XYZ full = end - start;
            double len = full.GetLength();
            XYZ dir = full.Normalize();
            double t = (p - start).DotProduct(dir);

            if (t >= -1e-6 && t <= len + 1e-6)
            {
                overshoot = 0;
                nearEnd = t <= len / 2.0 ? 0 : 1;
                return true;
            }

            if (t < 0)
            {
                overshoot = -t;
                nearEnd = 0;
            }
            else
            {
                overshoot = t - len;
                nearEnd = 1;
            }
            return false;
        }
    }
}
