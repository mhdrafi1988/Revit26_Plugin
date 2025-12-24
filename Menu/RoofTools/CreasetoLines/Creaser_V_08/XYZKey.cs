using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.Models
{
    /// <summary>
    /// Hashable XYZ wrapper for graph algorithms.
    /// </summary>
    public readonly struct CreaserCommand
    {
        public XYZ Point { get; }

        public CreaserCommand(XYZ point)
        {
            Point = point;
        }

        public double DistanceTo(CreaserCommand other)
        {
            return Point.DistanceTo(other.Point);
        }

        public override bool Equals(object obj)
        {
            if (obj is not CreaserCommand other)
                return false;

            return Point.IsAlmostEqualTo(other.Point);
        }

        public override int GetHashCode()
        {
            // Quantized hash for geometric stability
            int x = (int)(Point.X * 1000);
            int y = (int)(Point.Y * 1000);
            int z = (int)(Point.Z * 1000);
            return x ^ y ^ z;
        }
    }
}
