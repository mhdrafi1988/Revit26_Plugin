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
    }
}
