using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofEdgeCleanupService
    {
        public IList<FlattenedEdge2D> Clean(IList<FlattenedEdge2D> edges)
        {
            if (edges == null || edges.Count == 0)
                return new List<FlattenedEdge2D>();

            var counts = new Dictionary<UndirectedSegKey, int>();
            var reps = new Dictionary<UndirectedSegKey, FlattenedEdge2D>();

            foreach (var e in edges)
            {
                if (e == null) continue;

                var a = Snap2D(e.Start2D);
                var b = Snap2D(e.End2D);

                if (a.DistanceTo(b) < GeometryTolerance.Point)
                    continue;

                var key = UndirectedSegKey.Create(a, b);

                if (counts.ContainsKey(key))
                {
                    counts[key]++;
                }
                else
                {
                    counts[key] = 1;
                    reps[key] = new FlattenedEdge2D(a, b);
                }
            }

            var boundary =
                counts
                    .Where(kvp => kvp.Value == 1)
                    .Select(kvp => reps[kvp.Key])
                    .ToList();

            boundary = MergeCollinearTouching(boundary);

            return boundary;
        }

        private static XYZ Snap2D(XYZ p)
        {
            double t = GeometryTolerance.Point;
            double x = Math.Round(p.X / t) * t;
            double y = Math.Round(p.Y / t) * t;
            return new XYZ(x, y, 0);
        }

        private static List<FlattenedEdge2D> MergeCollinearTouching(List<FlattenedEdge2D> edges)
        {
            var comparer = new Point2DComparer();
            var unused = new List<FlattenedEdge2D>(edges);
            var result = new List<FlattenedEdge2D>();

            while (unused.Count > 0)
            {
                var current = unused[0];
                unused.RemoveAt(0);

                bool merged;
                do
                {
                    merged = false;

                    for (int i = unused.Count - 1; i >= 0; i--)
                    {
                        var cand = unused[i];
                        if (TryMerge(current, cand, comparer, out var mergedEdge))
                        {
                            current = mergedEdge;
                            unused.RemoveAt(i);
                            merged = true;
                        }
                    }
                }
                while (merged);

                result.Add(current);
            }

            return result;
        }

        private static bool TryMerge(
            FlattenedEdge2D a,
            FlattenedEdge2D b,
            IEqualityComparer<XYZ> comparer,
            out FlattenedEdge2D merged)
        {
            merged = null;

            if (!AreCollinear(a, b))
                return false;

            if (comparer.Equals(a.End2D, b.Start2D))
            {
                merged = new FlattenedEdge2D(a.Start2D, b.End2D);
                return true;
            }

            if (comparer.Equals(a.End2D, b.End2D))
            {
                merged = new FlattenedEdge2D(a.Start2D, b.Start2D);
                return true;
            }

            if (comparer.Equals(a.Start2D, b.End2D))
            {
                merged = new FlattenedEdge2D(b.Start2D, a.End2D);
                return true;
            }

            if (comparer.Equals(a.Start2D, b.Start2D))
            {
                merged = new FlattenedEdge2D(b.End2D, a.End2D);
                return true;
            }

            return false;
        }

        private static bool AreCollinear(FlattenedEdge2D a, FlattenedEdge2D b)
        {
            XYZ v1 = a.End2D - a.Start2D;
            XYZ v2 = b.End2D - b.Start2D;

            if (v1.GetLength() < GeometryTolerance.Point || v2.GetLength() < GeometryTolerance.Point)
                return false;

            v1 = v1.Normalize();
            v2 = v2.Normalize();

            return v1.CrossProduct(v2).GetLength() < GeometryTolerance.Point;
        }

        private readonly struct UndirectedSegKey : IEquatable<UndirectedSegKey>
        {
            private readonly long _ax;
            private readonly long _ay;
            private readonly long _bx;
            private readonly long _by;

            private UndirectedSegKey(long ax, long ay, long bx, long by)
            {
                _ax = ax; _ay = ay; _bx = bx; _by = by;
            }

            public static UndirectedSegKey Create(XYZ a, XYZ b)
            {
                long ax = Quant(a.X);
                long ay = Quant(a.Y);
                long bx = Quant(b.X);
                long by = Quant(b.Y);

                bool swap = ax > bx || (ax == bx && ay > by);
                if (swap)
                    return new UndirectedSegKey(bx, by, ax, ay);

                return new UndirectedSegKey(ax, ay, bx, by);
            }

            private static long Quant(double v)
            {
                double t = GeometryTolerance.Point;
                return (long)Math.Round(v / t);
            }

            public bool Equals(UndirectedSegKey other)
                => _ax == other._ax && _ay == other._ay && _bx == other._bx && _by == other._by;

            public override bool Equals(object obj)
                => obj is UndirectedSegKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = (h * 31) + _ax.GetHashCode();
                    h = (h * 31) + _ay.GetHashCode();
                    h = (h * 31) + _bx.GetHashCode();
                    h = (h * 31) + _by.GetHashCode();
                    return h;
                }
            }
        }
    }
}
