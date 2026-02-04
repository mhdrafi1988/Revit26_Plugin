using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit22_Plugin.RoofTag_V90
{
    /// <summary>
    /// Collision and intersection helpers for GeometryHelperV3.
    /// Kept private and isolated for clarity.
    /// </summary>
    public static partial class GeometryHelperV3
    {
        // ------------------------------------------------------------
        // Line vs polygon (2D XY)
        // ------------------------------------------------------------
        private static bool LineIntersectsPolygon(
            XYZ a,
            XYZ b,
            List<XYZ> poly)
        {
            for (int i = 0; i < poly.Count; i++)
            {
                XYZ p1 = poly[i];
                XYZ p2 = poly[(i + 1) % poly.Count];

                if (SegmentsIntersect2D(a, b, p1, p2))
                    return true;
            }
            return false;
        }

        private static bool SegmentsIntersect2D(
            XYZ p1,
            XYZ p2,
            XYZ p3,
            XYZ p4)
        {
            return DoIntersect(To2D(p1), To2D(p2), To2D(p3), To2D(p4));
        }

        // ------------------------------------------------------------
        // Robust 2D segment intersection
        // ------------------------------------------------------------
        private static bool DoIntersect(
            XY a,
            XY b,
            XY c,
            XY d)
        {
            return Orientation(a, b, c) != Orientation(a, b, d) &&
                   Orientation(c, d, a) != Orientation(c, d, b);
        }

        private static int Orientation(
            XY p,
            XY q,
            XY r)
        {
            double val =
                (q.Y - p.Y) * (r.X - q.X) -
                (q.X - p.X) * (r.Y - q.Y);

            if (Math.Abs(val) < 1e-9)
                return 0;

            return val > 0 ? 1 : 2;
        }

        // ------------------------------------------------------------
        // Lightweight 2D point
        // ------------------------------------------------------------
        private readonly struct XY
        {
            public readonly double X;
            public readonly double Y;

            public XY(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        private static XY To2D(XYZ p)
        {
            return new XY(p.X, p.Y);
        }
    }
}
