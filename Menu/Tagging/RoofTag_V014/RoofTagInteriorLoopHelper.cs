using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V014.Helpers
{
    /// <summary>
    /// Rule 2: per interior loop (an opening/hole in the roof's footprint sketch,
    /// as opposed to the outer boundary), keep only the top 4 highest points —
    /// or fewer if the loop has fewer than 4 candidate points. Those points are
    /// kept as a group only if each consecutive pair (ordered by height) is at
    /// least the configured horizontal spacing apart; otherwise the loop
    /// collapses to its single highest point. Points that don't sit on any
    /// interior loop (outer boundary, or a roof with no interior loops) pass
    /// through untouched.
    ///
    /// Loop source: FootPrintRoof.GetProfile(). The loop with the largest
    /// plan-projected area is treated as the outer boundary; every other loop
    /// is interior. Requires a FootPrintRoof — other roof types have no
    /// GetProfile() and are passed through unfiltered.
    /// </summary>
    public static class RoofTagInteriorLoopHelper
    {
        // Fixed match tolerance for "point sits on this loop's boundary" — not exposed in UI.
        private const double OnLoopTolFt = 50.0 / 304.8; // 50 mm

        public static List<XYZ> Filter(
            RoofBase roof,
            List<XYZ> points,
            double spacingFt,
            out List<XYZ> skipped,
            out List<string> loopLogs)
        {
            skipped = new List<XYZ>();
            loopLogs = new List<string>();

            if (roof is not FootPrintRoof fpRoof)
            {
                loopLogs.Add("Interior loop reduction skipped — roof is not a FootPrintRoof (no sketch profile available).");
                return new List<XYZ>(points);
            }

            ModelCurveArrArray profile;
            try
            {
                profile = fpRoof.GetProfiles();
            }
            catch
            {
                profile = null;
            }

            if (profile == null || profile.IsEmpty)
            {
                loopLogs.Add("Interior loop reduction skipped — roof sketch profile unavailable.");
                return new List<XYZ>(points);
            }

            // ── Build a plan polyline + area for every sketch loop ──────────
            var loops = new List<(List<XYZ> Polyline, double Area)>();
            foreach (ModelCurveArray loopArr in profile)
            {
                List<XYZ> poly = BuildPolyline(loopArr);
                if (poly.Count < 2) continue;
                loops.Add((poly, ShoelaceArea(poly)));
            }

            if (loops.Count <= 1)
            {
                loopLogs.Add("No interior loops found on this roof — rule 2 skipped.");
                return new List<XYZ>(points);
            }

            // Largest-area loop = outer boundary; the rest are interior loops.
            int outerIndex = loops
                .Select((l, i) => (l.Area, i))
                .OrderByDescending(t => t.Area)
                .First().i;

            var interiorLoops = loops
                .Where((_, i) => i != outerIndex)
                .Select(l => l.Polyline)
                .ToList();

            // status: 0 = untouched (outer/unmatched), 1 = keep, 2 = skip
            int[] status = new int[points.Count];
            bool[] claimed = new bool[points.Count];

            int loopNumber = 0;
            foreach (List<XYZ> loopPoly in interiorLoops)
            {
                loopNumber++;

                var matchedIdx = new List<int>();
                for (int i = 0; i < points.Count; i++)
                {
                    if (claimed[i]) continue;
                    if (DistanceToPolylineXY(points[i], loopPoly) <= OnLoopTolFt)
                        matchedIdx.Add(i);
                }

                if (matchedIdx.Count == 0)
                {
                    loopLogs.Add($"Interior loop {loopNumber} — no matching points found.");
                    continue;
                }

                foreach (int i in matchedIdx) claimed[i] = true;

                // Sort matched points by height, descending. OrderByDescending is
                // stable, so ties keep original input order (per tie-break rule).
                List<int> sortedIdx = matchedIdx
                    .OrderByDescending(i => points[i].Z)
                    .ToList();

                int topCount = Math.Min(4, sortedIdx.Count);
                List<int> topIdx = sortedIdx.Take(topCount).ToList();
                List<int> beyondTop = sortedIdx.Skip(topCount).ToList();

                bool allSpaced = true;
                for (int i = 0; i < topIdx.Count - 1; i++)
                {
                    double horizDist = HorizontalDistance(points[topIdx[i]], points[topIdx[i + 1]]);
                    if (horizDist < spacingFt)
                    {
                        allSpaced = false;
                        break;
                    }
                }

                List<int> survivors;
                List<int> dropped = new List<int>(beyondTop);

                if (allSpaced)
                {
                    survivors = topIdx;
                }
                else
                {
                    survivors = new List<int> { topIdx[0] };
                    dropped.AddRange(topIdx.Skip(1));
                }

                foreach (int i in survivors) status[i] = 1;
                foreach (int i in dropped) status[i] = 2;

                double spacingMm = UnitUtils.ConvertFromInternalUnits(spacingFt, UnitTypeId.Millimeters);
                loopLogs.Add(
                    $"Interior loop {loopNumber} — {matchedIdx.Count} pt(s) on loop, " +
                    $"top {topIdx.Count} checked, kept {survivors.Count} " +
                    $"({(allSpaced ? "spacing OK" : $"below {spacingMm:0} mm spacing → highest only")})");
            }

            var result = new List<XYZ>();
            for (int i = 0; i < points.Count; i++)
            {
                if (status[i] == 2)
                    skipped.Add(points[i]);
                else
                    result.Add(points[i]);
            }

            return result;
        }

        private static List<XYZ> BuildPolyline(ModelCurveArray loopArr)
        {
            var poly = new List<XYZ>();
            foreach (ModelCurve mc in loopArr)
            {
                Curve c = mc?.GeometryCurve;
                if (c == null) continue;

                IList<XYZ> tess;
                try { tess = c.Tessellate(); }
                catch { continue; }

                foreach (XYZ p in tess)
                {
                    if (poly.Count == 0 || poly[^1].DistanceTo(p) > 1e-6)
                        poly.Add(p);
                }
            }
            return poly;
        }

        private static double ShoelaceArea(List<XYZ> poly)
        {
            double sum = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                XYZ a = poly[i];
                XYZ b = poly[(i + 1) % poly.Count];
                sum += (a.X * b.Y) - (b.X * a.Y);
            }
            return Math.Abs(sum) * 0.5;
        }

        private static double DistanceToPolylineXY(XYZ pt, List<XYZ> poly)
        {
            double min = double.MaxValue;
            for (int i = 0; i < poly.Count - 1; i++)
            {
                double d = DistancePointToSegmentXY(pt, poly[i], poly[i + 1]);
                if (d < min) min = d;
            }
            return min;
        }

        private static double DistancePointToSegmentXY(XYZ p, XYZ a, XYZ b)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y;
            double apx = p.X - a.X, apy = p.Y - a.Y;
            double lenSq = abx * abx + aby * aby;
            double t = lenSq < 1e-12 ? 0 : (apx * abx + apy * aby) / lenSq;
            t = Math.Max(0, Math.Min(1, t));
            double cx = a.X + t * abx, cy = a.Y + t * aby;
            double dx = p.X - cx, dy = p.Y - cy;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double HorizontalDistance(XYZ a, XYZ b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
