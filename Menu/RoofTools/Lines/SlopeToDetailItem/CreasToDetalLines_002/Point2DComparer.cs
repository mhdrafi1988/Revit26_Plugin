using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    internal class Point2DComparer : IEqualityComparer<XYZ>
    {
        public bool Equals(XYZ a, XYZ b)
        {
            if (a == null || b == null) return false;

            return a.DistanceTo(b) < GeometryTolerance.Point;
        }

        public int GetHashCode(XYZ p)
        {
            unchecked
            {
                int x = p.X.GetHashCode();
                int y = p.Y.GetHashCode();
                return x ^ y;
            }
        }
    }
}
