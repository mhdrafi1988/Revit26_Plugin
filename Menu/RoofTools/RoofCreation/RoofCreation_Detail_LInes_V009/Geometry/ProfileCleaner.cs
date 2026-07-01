using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.V009.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.V009.Geometry
{
    public static class ProfileCleaner
    {
        public static List<CurveLoop> CleanAndBuildLoops(
            List<Curve> roofCurves,
            List<ProfileLoop> floorProfiles)
        {
            var allCurves = new List<Curve>();
            allCurves.AddRange(roofCurves);

            foreach (var fp in floorProfiles)
                allCurves.AddRange(fp.Curves);

            // Snap endpoints while preserving curve type
            allCurves = SnapEndpoints(allCurves);

            return BuildClosedLoops(allCurves);
        }

        private static List<Curve> SnapEndpoints(List<Curve> curves)
        {
            var snapped = new List<Curve>();

            foreach (var c in curves)
            {
                // Unbound curves (full circles) have no endpoints — skip snapping,
                // pass through as-is. BuildClosedLoops handles them via IsSelfClosed().
                if (!c.IsBound)
                {
                    snapped.Add(c);
                    continue;
                }

                XYZ p0 = c.GetEndPoint(0);
                XYZ p1 = c.GetEndPoint(1);

                foreach (var other in curves)
                {
                    if (ReferenceEquals(c, other)) continue;
                    if (!other.IsBound) continue;

                    p0 = CurveUtils.SnapPoint(p0, other.GetEndPoint(0));
                    p0 = CurveUtils.SnapPoint(p0, other.GetEndPoint(1));
                    p1 = CurveUtils.SnapPoint(p1, other.GetEndPoint(0));
                    p1 = CurveUtils.SnapPoint(p1, other.GetEndPoint(1));
                }

                snapped.Add(RebuildWithNewEndpoints(c, p0, p1));
            }

            return snapped;
        }

        /// <summary>
        /// Rebuilds a curve with snapped endpoints while keeping its geometric type.
        /// For lines: new bound. For arcs/splines: if snap delta is within tolerance
        /// (which it always is by design) the original geometry is still valid — return as-is.
        /// </summary>
        private static Curve RebuildWithNewEndpoints(Curve original, XYZ p0, XYZ p1)
        {
            if (original is Line)
                return Line.CreateBound(p0, p1);

            // For arcs, splines, ellipses: the snap delta is sub-millimetre by definition.
            // The original curve geometry is still valid — return it unchanged.
            // Loop closure will still pass because ArePointsClose() uses the same tolerance.
            return original;
        }

        private static List<CurveLoop> BuildClosedLoops(List<Curve> curves)
        {
            var loops  = new List<CurveLoop>();
            var unused = new List<Curve>(curves);

            while (unused.Any())
            {
                Curve current = unused.First();
                unused.Remove(current);

                // A full circle (or closed ellipse) has coincident start/end points
                // and forms a valid loop on its own — no chain needed.
                if (IsSelfClosed(current))
                {
                    var selfLoop = new CurveLoop();
                    selfLoop.Append(current);
                    loops.Add(selfLoop);
                    continue;
                }

                var loop = new CurveLoop();
                loop.Append(current);

                XYZ end    = current.GetEndPoint(1);
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
                        loop.First().GetEndPoint(0), end);
                }

                // >= 3 guard is for line chains only — a single self-closed curve
                // is handled above, so this path always has 2+ segments minimum.
                if (closed && loop.Count() >= 2)
                    loops.Add(loop);
            }

            return loops;
        }

        /// <summary>
        /// Returns true for curves whose start and end point coincide —
        /// i.e. a full circle, full ellipse, or closed spline.
        /// These are valid CurveLoop members on their own.
        /// </summary>
        private static bool IsSelfClosed(Curve curve)
        {
            // Unbound curves (true full circles from Revit) have no endpoints
            if (!curve.IsBound)
                return true;

            return CurveUtils.ArePointsClose(
                curve.GetEndPoint(0),
                curve.GetEndPoint(1));
        }
    }
}
