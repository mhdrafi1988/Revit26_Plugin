// ============================================================
// File: EdgeSegment.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal readonly struct EdgeSegment
    {
        public XYZKey A { get; }
        public XYZKey B { get; }

        public EdgeSegment(XYZKey a, XYZKey b)
        {
            A = a;
            B = b;
        }
    }
}
