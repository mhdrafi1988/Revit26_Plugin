using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.V5_00.Domain.Services
{
    /// <summary>
    /// Cleans a set of 3D line segments:
    /// - Removes duplicates (order independent).
    /// - Removes segments collinear with already kept segments.
    /// </summary>
    public class SegmentCleanupService
    {
        public List<(XYZ, XYZ)> CleanSegments(List<(XYZ, XYZ)> raw)
        {
            if (raw == null)
                return new List<(XYZ, XYZ)>();

            // 1) Normalize and deduplicate
            var unique = new Dictionary<string, (XYZ, XYZ)>();

            foreach (var seg in raw)
            {
                var a = seg.Item1;
                var b = seg.Item2;

                string key;
                if (IsFirstBeforeSecond(a, b))
                    key = KeyFor(a, b);
                else
                    key = KeyFor(b, a);

                if (!unique.ContainsKey(key))
                    unique[key] = seg;
            }

            // 2) Remove collinear segments
            var nonCollinear = new List<(XYZ, XYZ)>();

            foreach (var seg in unique.Values)
            {
                bool isCollinear = false;

                foreach (var kept in nonCollinear)
                {
                    if (AreCollinear(kept.Item1, kept.Item2, seg.Item1) &&
                        AreCollinear(kept.Item1, kept.Item2, seg.Item2))
                    {
                        isCollinear = true;
                        break;
                    }
                }

                if (!isCollinear)
                    nonCollinear.Add(seg);
            }

            return nonCollinear;
        }

        private bool IsFirstBeforeSecond(XYZ a, XYZ b)
        {
            if (a.X != b.X) return a.X < b.X;
            if (a.Y != b.Y) return a.Y < b.Y;
            return a.Z < b.Z;
        }

        private string KeyFor(XYZ a, XYZ b)
        {
            return $"{a.X:F6},{a.Y:F6},{a.Z:F6}-{b.X:F6},{b.Y:F6},{b.Z:F6}";
        }

        private bool AreCollinear(XYZ a, XYZ b, XYZ c, double tol = 1e-6)
        {
            XYZ ab = b - a;
            XYZ ac = c - a;
            XYZ cross = ab.CrossProduct(ac);
            return cross.GetLength() < tol;
        }
    }
}
