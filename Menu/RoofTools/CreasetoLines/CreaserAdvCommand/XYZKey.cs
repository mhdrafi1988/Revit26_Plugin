using System;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_adv_V001.Helpers
{
    /// <summary>
    /// Stable, tolerance-based XYZ key for dictionary usage.
    /// </summary>
    public readonly struct XYZKey : IEquatable<XYZKey>
    {
        private const double Tol = 1e-6;

        private readonly long _x;
        private readonly long _y;
        private readonly long _z;

        public XYZKey(XYZ p)
        {
            _x = (long)Math.Round(p.X / Tol);
            _y = (long)Math.Round(p.Y / Tol);
            _z = (long)Math.Round(p.Z / Tol);
        }

        public bool Equals(XYZKey other)
            => _x == other._x && _y == other._y && _z == other._z;

        public override bool Equals(object obj)
            => obj is XYZKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(_x, _y, _z);
    }
}
