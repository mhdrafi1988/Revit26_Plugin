using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.Creaser_V03_03.Models
{
    public readonly struct XYZKey
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

        public override bool Equals(object obj) =>
            obj is XYZKey other &&
            X == other.X && Y == other.Y && Z == other.Z;

        public override int GetHashCode() =>
            HashCode.Combine(X, Y, Z);
        public double DistanceTo(XYZKey other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

    }
}
