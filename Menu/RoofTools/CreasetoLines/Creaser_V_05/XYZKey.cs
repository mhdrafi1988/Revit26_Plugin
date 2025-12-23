using System;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V05.Commands
{
    internal readonly struct XYZKey : IEquatable<XYZKey>
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

        public XYZKey(double x, double y, double z)
        {
            X = Math.Round(x, 6);
            Y = Math.Round(y, 6);
            Z = Math.Round(z, 6);
        }

        public XYZ ToXYZ() => new XYZ(X, Y, Z);

        public double DistanceTo(XYZKey o)
        {
            double dx = X - o.X;
            double dy = Y - o.Y;
            double dz = Z - o.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override bool Equals(object obj) =>
            obj is XYZKey k && Equals(k);

        public bool Equals(XYZKey other) =>
            Math.Abs(X - other.X) < 1e-6 &&
            Math.Abs(Y - other.Y) < 1e-6 &&
            Math.Abs(Z - other.Z) < 1e-6;

        public static bool operator ==(XYZKey left, XYZKey right) => left.Equals(right);
        public static bool operator !=(XYZKey left, XYZKey right) => !left.Equals(right);

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }
}