using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V32.Models
{
    public class DirectedLine
    {
        public XYZ P1 { get; }
        public XYZ P2 { get; }

        public bool IsZeroLength => P1.IsAlmostEqualTo(P2);

        public DirectedLine(XYZ p1, XYZ p2)
        {
            P1 = p1;
            P2 = p2;
        }

        public override int GetHashCode() =>
            P1.GetHashCode() ^ P2.GetHashCode();

        public override bool Equals(object obj)
        {
            if (obj is not DirectedLine other)
                return false;

            return P1.IsAlmostEqualTo(other.P1) &&
                   P2.IsAlmostEqualTo(other.P2);
        }
    }
}
