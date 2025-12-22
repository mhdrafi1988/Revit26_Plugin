// ============================================================
// File: EdgeKey.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

using System;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly XYZKey A;
        public readonly XYZKey B;

        public EdgeKey(XYZKey p1, XYZKey p2)
        {
            // Direction independent (hash ordering)
            if (p1.GetHashCode() <= p2.GetHashCode())
            {
                A = p1;
                B = p2;
            }
            else
            {
                A = p2;
                B = p1;
            }
        }

        public bool Equals(EdgeKey other) =>
            A.Equals(other.A) && B.Equals(other.B);

        public override bool Equals(object obj) =>
            obj is EdgeKey e && Equals(e);

        public override int GetHashCode() =>
            HashCode.Combine(A, B);
    }
}
