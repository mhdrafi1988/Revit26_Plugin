using Autodesk.Revit.DB;
using System;

namespace Revit22_Plugin.RoofTagV4.Helpers
{
    public static class XYZExtensions
    {
        // ==============================================================
        // Safe normalization
        // ==============================================================
        public static XYZ NormalizeSafe(this XYZ v)
        {
            double len = v.GetLength();
            if (len < 1e-9) return XYZ.Zero;
            return v / len;
        }

        // ==============================================================
        // Remove Z component
        // ==============================================================
        public static XYZ FlattenZ(this XYZ v)
        {
            return new XYZ(v.X, v.Y, 0);
        }

        // ==============================================================
        // 2D Distance
        // ==============================================================
        public static double Distance2D(this XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // ==============================================================
        // Convert to 2D struct
        // ==============================================================
        public struct XY
        {
            public double X, Y;
            public XY(double x, double y) { X = x; Y = y; }
        }

        public static XY ToXY(this XYZ p)
        {
            return new XY(p.X, p.Y);
        }

        // ==============================================================
        // Zero-length check
        // ==============================================================
        public static bool IsZeroVector(this XYZ v)
        {
            return v.GetLength() < 1e-6;
        }

        // ==============================================================
        // Dot product (2D)
        // ==============================================================
        public static double Dot2D(this XYZ a, XYZ b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        // ==============================================================
        // 2D perpendicular vector
        // ==============================================================
        public static XYZ Perp2D(this XYZ v)
        {
            return new XYZ(-v.Y, v.X, 0);
        }

        // ==============================================================
        // Rotate vector in XY plane by degrees
        // ==============================================================
        public static XYZ Rotate2D(this XYZ v, double deg)
        {
            double rad = deg * Math.PI / 180.0;

            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double nx = v.X * cos - v.Y * sin;
            double ny = v.X * sin + v.Y * cos;

            return new XYZ(nx, ny, 0);
        }
    }
}
