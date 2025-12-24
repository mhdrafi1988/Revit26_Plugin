using System;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V31.Models
{
    /// <summary>
    /// Immutable, hash-safe replacement for XYZ
    /// used as dictionary and graph keys.
    /// </summary>
    public readonly struct XYZKey  // Changed from internal to public
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public XYZKey(XYZ p)
        {
            X = Math.Round(p.X, 6);
            Y = Math.Round(p.Y, 6);
            Z = Math.Round(p.Z, 6);
        }

        public XYZ ToXYZ() => new XYZ(X, Y, Z);

        public double DistanceTo(XYZKey other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override int GetHashCode()
            => HashCode.Combine(X, Y, Z);

        public override bool Equals(object obj)
            => obj is XYZKey k
               && Math.Abs(X - k.X) < 1e-9
               && Math.Abs(Y - k.Y) < 1e-9
               && Math.Abs(Z - k.Z) < 1e-9;
    }
}