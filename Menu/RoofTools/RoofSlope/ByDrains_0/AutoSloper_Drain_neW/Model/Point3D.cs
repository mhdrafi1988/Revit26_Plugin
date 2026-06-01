// =======================================================
// File: Models/Point3D.cs
// Description: UI-friendly point representation (mm)
// =======================================================

using System;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Models
{
    /// <summary>
    /// UI-friendly 3D point in millimeters (no Revit API dependency)
    /// </summary>
    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D()
        {
            X = Y = Z = 0;
        }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2}, {Z:F2})";
        }

        public bool Equals(Point3D other)
        {
            if (other == null) return false;
            return Math.Abs(X - other.X) < 0.001 &&
                   Math.Abs(Y - other.Y) < 0.001 &&
                   Math.Abs(Z - other.Z) < 0.001;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Point3D);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }
    }
}