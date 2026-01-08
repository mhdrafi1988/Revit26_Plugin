// File: PerpendicularLineService.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Services.Geometry
//
// Responsibility:
// - Computes midpoint and perpendicular vectors
// - Pure geometry math (API-light)
// - No transactions

using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofRidgeLines_V06.Services.Geometry
{
    public class PerpendicularLineService
    {
        /// <summary>
        /// Computes midpoint between two points.
        /// </summary>
        public XYZ GetMidPoint(XYZ p1, XYZ p2)
        {
            return (p1 + p2) * 0.5;
        }

        /// <summary>
        /// Gets normalized perpendicular direction in XY plane.
        /// </summary>
        public XYZ GetPerpendicularDirection(XYZ direction)
        {
            XYZ dirXY = new XYZ(direction.X, direction.Y, 0).Normalize();

            // Rotate 90 degrees in XY
            return new XYZ(-dirXY.Y, dirXY.X, 0);
        }
    }
}
