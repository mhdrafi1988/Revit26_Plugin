using Autodesk.Revit.DB;
using System;
using Revit26_Plugin.CSFL_V07.Models;

namespace Revit26_Plugin.CSFL_V07.Services.Geometry
{
    /// <summary>
    /// Produces stable, readable section orientation from a detail line.
    /// </summary>
    public class SectionOrientationService
    {
        public OrientationResult Calculate(Line line)
        {
            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);

            XYZ dir = (p1 - p0).Normalize();

            // Normalize direction for predictable orientation
            if (Math.Abs(dir.X) < Math.Abs(dir.Y))
                dir = dir.Y < 0 ? dir.Negate() : dir;
            else
                dir = dir.X < 0 ? dir.Negate() : dir;

            XYZ zDir = new XYZ(-dir.Y, dir.X, 0).Normalize();
            XYZ yDir = XYZ.BasisZ;
            XYZ xDir = yDir.CrossProduct(zDir).Normalize();

            return new OrientationResult
            {
                XDir = xDir,
                YDir = yDir,
                ZDir = zDir,
                MidPoint = (p0 + p1) / 2.0,
                Success = true
            };
        }
    }
}
