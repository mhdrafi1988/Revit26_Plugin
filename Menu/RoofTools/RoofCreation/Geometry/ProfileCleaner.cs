using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.Geometry
{
    public static class ProfileCleaner
    {
        public static List<CurveLoop> CleanAndBuildLoops(
            List<Curve> roofCurves,
            List<ProfileLoop> floorProfiles)
        {
            // Roof is authoritative
            var allCurves = new List<Curve>();
            allCurves.AddRange(roofCurves);

            foreach (var fp in floorProfiles)
                allCurves.AddRange(fp.Curves);

            // 1?? Remove overlapping curves
            allCurves = RemoveOverlaps(allCurves, roofCurves);

            // 2?? Snap endpoints
            allCurves = SnapEndpoints(allCurves);

            // 3?? Build closed loops
            return BuildClosedLoops(allCurves);
        }

        // --------------------------------------------------

        private static List<Curve> RemoveOverlaps(
            List<Curve> curves,
            List<Curve> roofCurves)
        {
            var result = new List<Curve>();

            foreach (var c in curves)
            {
                bool overlap = result.Any(r => CurveUtils.AreCurvesOverlapping(r, c));

                if (!overlap)
                {
                    result.Add(c);
                }
                else
                {
                    // Prefer roof curve
                    bool isRoofCurve = roofCurves.Any(r => CurveUtils.AreCurvesOverlapping(r, c));
                    if (isRoofCurve)
                        result.Add(c);
                }
            }

            return result;
        }

        // --------------------------------------------------

        private static List<Curve> SnapEndpoints(List<Curve> curves)
        {
            var snapped = new List<Curve>();

            foreach (var c in curves)
            {
                XYZ p0 = c.GetEndPoint(0);
                XYZ p1 = c.GetEndPoint(1);

                foreach (var other in curves)
                {
                    if (c == other) continue;

                    p0 = CurveUtils.SnapPoint(p0, other.GetEndPoint(0));
                    p0 = CurveUtils.SnapPoint(p0, other.GetEndPoint(1));
                    p1 = CurveUtils.SnapPoint(p1, other.GetEndPoint(0));
                    p1 = CurveUtils.SnapPoint(p1, other.GetEndPoint(1));
                }

                snapped.Add(Line.CreateBound(p0, p1));
            }

            return snapped;
        }

        // --------------------------------------------------

        private static List<CurveLoop> BuildClosedLoops(List<Curve> curves)
        {
            var loops = new List<CurveLoop>();
            var unused = new List<Curve>(curves);

            while (unused.Any())
            {
                var loop = new CurveLoop();
                Curve current = unused.First();
                unused.Remove(current);

                loop.Append(current);

                XYZ end = current.GetEndPoint(1);
                bool closed = false;

                while (!closed)
                {
                    Curve next = unused
                        .FirstOrDefault(c =>
                            CurveUtils.ArePointsClose(c.GetEndPoint(0), end) ||
                            CurveUtils.ArePointsClose(c.GetEndPoint(1), end));

                    if (next == null)
                        break;

                    unused.Remove(next);

                    if (CurveUtils.ArePointsClose(next.GetEndPoint(1), end))
                        next = next.CreateReversed();

                    loop.Append(next);
                    end = next.GetEndPoint(1);

                    closed = CurveUtils.ArePointsClose(
                        loop.First().GetEndPoint(0),
                        end);
                }

                if (closed && loop.Count() >= 3)
                    loops.Add(loop);
            }

            return loops;
        }
    }
}
