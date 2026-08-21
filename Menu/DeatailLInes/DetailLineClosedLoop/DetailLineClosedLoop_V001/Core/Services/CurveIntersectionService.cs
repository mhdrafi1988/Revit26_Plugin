using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Revit26_Plugin.DetailLineClosedLoop.V001.Core.Geometry;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Services
{
    /// <summary>
    /// Step 2 — trims overshooting curves back to their true intersection and
    /// extends short curves out to meet a neighbor, within tolerance. Only the
    /// endpoint nearer to the intersection point moves, and only if that
    /// endpoint isn't already anchored to a third curve (protects existing
    /// good joints from being pulled apart by an unrelated pairing).
    /// </summary>
    public static class CurveIntersectionService
    {
        public static List<Curve> TrimAndExtend(List<Curve> curves, double snapTolerance, double extendLimit, ObservableCollection<LogEntry> log)
        {
            var working = new List<Curve>(curves);
            int trimmed = 0;
            int extended = 0;

            for (int i = 0; i < working.Count; i++)
            {
                for (int j = i + 1; j < working.Count; j++)
                {
                    Curve a = working[i];
                    Curve b = working[j];

#pragma warning disable CS0618 // Revit 2026 deprecated this overload in favor of Intersect(Curve, CurveIntersectResultOption) — kept until that replacement's exact signature is confirmed against the installed API.
                    SetComparisonResult res = a.Intersect(b, out IntersectionResultArray results);
#pragma warning restore CS0618
                    if (res == SetComparisonResult.Overlap && results != null)
                    {
                        foreach (IntersectionResult ir in results)
                        {
                            XYZ pt = ir.XYZPoint;
                            if (TryTrimEndpoint(working, i, pt, snapTolerance)) trimmed++;
                            if (TryTrimEndpoint(working, j, pt, snapTolerance)) trimmed++;
                        }
                    }
                    else if (working[i] is Line la && working[j] is Line lb)
                    {
                        XYZ pt = LineExtensionUtils.IntersectInfinite(la, lb, snapTolerance);
                        if (pt != null)
                        {
                            if (TryTrimEndpoint(working, i, pt, extendLimit)) extended++;
                            if (TryTrimEndpoint(working, j, pt, extendLimit)) extended++;
                        }
                    }
                }
            }

            if (trimmed > 0 || extended > 0)
                log.Add(new LogEntry(LogLevel.Debug, $"Trim/extend pass: {trimmed} trim(s), {extended} extension(s) resolved"));

            return working;
        }

        private static bool TryTrimEndpoint(List<Curve> curves, int index, XYZ target, double maxMove)
        {
            Curve c = curves[index];
            XYZ p0 = c.GetEndPoint(0);
            XYZ p1 = c.GetEndPoint(1);

            double d0 = p0.DistanceTo(target);
            double d1 = p1.DistanceTo(target);
            int nearer = d0 <= d1 ? 0 : 1;
            double moveDist = nearer == 0 ? d0 : d1;

            if (moveDist < 1e-9 || moveDist > maxMove)
                return false;

            XYZ farPoint = nearer == 0 ? p1 : p0;
            if (farPoint.DistanceTo(target) < 1e-6)
                return false; // would collapse to zero length

            if (IsEndpointShared(curves, index, nearer == 0 ? p0 : p1, maxMove))
                return false; // don't disturb an endpoint already anchored elsewhere

            Curve rebuilt = nearer == 0 ? RebuildCurve(c, target, p1) : RebuildCurve(c, p0, target);
            if (rebuilt == null)
                return false;

            curves[index] = rebuilt;
            return true;
        }

        private static bool IsEndpointShared(List<Curve> curves, int selfIndex, XYZ point, double tolerance)
        {
            for (int k = 0; k < curves.Count; k++)
            {
                if (k == selfIndex) continue;
                Curve other = curves[k];
                if (other.GetEndPoint(0).DistanceTo(point) <= tolerance) return true;
                if (other.GetEndPoint(1).DistanceTo(point) <= tolerance) return true;
            }
            return false;
        }

        private static Curve RebuildCurve(Curve original, XYZ newStart, XYZ newEnd)
        {
            if (original is Line)
                return Line.CreateBound(newStart, newEnd);

            if (original is Arc arc)
            {
                XYZ midPoint = arc.Evaluate(0.5, true);
                return Arc.Create(newStart, newEnd, midPoint);
            }

            return null;
        }
    }
}
