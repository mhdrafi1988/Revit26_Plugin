using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class PathCleanupService
    {
        public IList<Line> Clean(IList<Line> lines)
        {
            if (lines == null || lines.Count == 0)
                return new List<Line>();

            var filtered =
                lines
                    .Where(l => l != null && l.Length > GeometryTolerance.Point)
                    .ToList();

            var unique = RemoveDuplicates(filtered);
            var merged = MergeCollinear(unique);

            return merged;
        }

        private IList<Line> RemoveDuplicates(IList<Line> lines)
        {
            var result = new List<Line>();

            foreach (var l in lines)
            {
                bool exists =
                    result.Any(r =>
                        (SamePoint(r.GetEndPoint(0), l.GetEndPoint(0)) &&
                         SamePoint(r.GetEndPoint(1), l.GetEndPoint(1))) ||
                        (SamePoint(r.GetEndPoint(0), l.GetEndPoint(1)) &&
                         SamePoint(r.GetEndPoint(1), l.GetEndPoint(0))));

                if (!exists)
                    result.Add(l);
            }

            return result;
        }

        private IList<Line> MergeCollinear(IList<Line> lines)
        {
            var remaining = new List<Line>(lines);
            var result = new List<Line>();

            while (remaining.Count > 0)
            {
                var current = remaining[0];
                remaining.RemoveAt(0);

                bool merged;
                do
                {
                    merged = false;

                    for (int i = remaining.Count - 1; i >= 0; i--)
                    {
                        if (TryMerge(current, remaining[i], out var mergedLine))
                        {
                            current = mergedLine;
                            remaining.RemoveAt(i);
                            merged = true;
                        }
                    }
                }
                while (merged);

                result.Add(current);
            }

            return result;
        }

        private bool TryMerge(Line a, Line b, out Line merged)
        {
            merged = null;

            XYZ aDir = (a.GetEndPoint(1) - a.GetEndPoint(0)).Normalize();
            XYZ bDir = (b.GetEndPoint(1) - b.GetEndPoint(0)).Normalize();

            if (aDir.CrossProduct(bDir).GetLength() > GeometryTolerance.Point)
                return false;

            if (SamePoint(a.GetEndPoint(1), b.GetEndPoint(0)))
            {
                merged = Line.CreateBound(a.GetEndPoint(0), b.GetEndPoint(1));
                return true;
            }

            if (SamePoint(a.GetEndPoint(1), b.GetEndPoint(1)))
            {
                merged = Line.CreateBound(a.GetEndPoint(0), b.GetEndPoint(0));
                return true;
            }

            if (SamePoint(a.GetEndPoint(0), b.GetEndPoint(1)))
            {
                merged = Line.CreateBound(b.GetEndPoint(0), a.GetEndPoint(1));
                return true;
            }

            if (SamePoint(a.GetEndPoint(0), b.GetEndPoint(0)))
            {
                merged = Line.CreateBound(b.GetEndPoint(1), a.GetEndPoint(1));
                return true;
            }

            return false;
        }

        private bool SamePoint(XYZ a, XYZ b)
        {
            return a.DistanceTo(b) < GeometryTolerance.Point;
        }
    }
}
