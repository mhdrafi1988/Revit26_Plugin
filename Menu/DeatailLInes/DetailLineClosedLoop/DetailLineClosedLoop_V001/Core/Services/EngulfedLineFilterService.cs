using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Services
{
    /// <summary>
    /// Step 4 — safety net that runs after merging: drops exact-duplicate
    /// curves (any type) and any Line whose full span is contained inside
    /// another remaining Line on the same infinite line (redundant interior
    /// segment that doesn't contribute to the outer perimeter).
    /// </summary>
    public static class EngulfedLineFilterService
    {
        public static List<Curve> RemoveEngulfed(List<Curve> curves, double tolerance, out int removedCount, ObservableCollection<LogEntry> log)
        {
            var removed = new bool[curves.Count];

            for (int i = 0; i < curves.Count; i++)
            {
                if (removed[i]) continue;
                for (int j = i + 1; j < curves.Count; j++)
                {
                    if (removed[j]) continue;
                    if (AreDuplicate(curves[i], curves[j], tolerance))
                        removed[j] = true;
                }
            }

            for (int i = 0; i < curves.Count; i++)
            {
                if (removed[i] || curves[i] is not Line inner) continue;
                for (int j = 0; j < curves.Count; j++)
                {
                    if (i == j || removed[j] || curves[j] is not Line outer) continue;
                    if (IsCollinearContained(inner, outer, tolerance))
                    {
                        removed[i] = true;
                        break;
                    }
                }
            }

            var result = new List<Curve>();
            int count = 0;
            for (int i = 0; i < curves.Count; i++)
            {
                if (removed[i]) count++;
                else result.Add(curves[i]);
            }

            removedCount = count;
            if (count > 0)
                log.Add(new LogEntry(LogLevel.Warning, $"Removed {count} fully-engulfed/duplicate interior line(s)"));

            return result;
        }

        private static bool AreDuplicate(Curve a, Curve b, double tolerance)
        {
            XYZ a0 = a.GetEndPoint(0), a1 = a.GetEndPoint(1);
            XYZ b0 = b.GetEndPoint(0), b1 = b.GetEndPoint(1);

            bool sameOrder = a0.DistanceTo(b0) <= tolerance && a1.DistanceTo(b1) <= tolerance;
            bool reversed = a0.DistanceTo(b1) <= tolerance && a1.DistanceTo(b0) <= tolerance;
            return sameOrder || reversed;
        }

        private static bool IsCollinearContained(Line inner, Line outer, double tolerance)
        {
            XYZ dir = outer.Direction.Normalize();
            XYZ origin = outer.GetEndPoint(0);

            double offsetDist = (inner.GetEndPoint(0) - origin).CrossProduct(dir).GetLength();
            if (offsetDist > tolerance) return false;

            double innerT0 = (inner.GetEndPoint(0) - origin).DotProduct(dir);
            double innerT1 = (inner.GetEndPoint(1) - origin).DotProduct(dir);
            double outerT1 = (outer.GetEndPoint(1) - origin).DotProduct(dir);

            double innerMin = Math.Min(innerT0, innerT1);
            double innerMax = Math.Max(innerT0, innerT1);
            double outerMin = Math.Min(0, outerT1);
            double outerMax = Math.Max(0, outerT1);

            return innerMin >= outerMin - tolerance && innerMax <= outerMax + tolerance;
        }
    }
}
