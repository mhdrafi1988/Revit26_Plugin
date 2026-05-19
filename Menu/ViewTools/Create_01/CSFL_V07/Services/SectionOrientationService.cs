using Autodesk.Revit.DB;
using System;
using Revit26_Plugin.CSFL_V07.Models;

namespace Revit26_Plugin.CSFL_V07.Services.Geometry
{
    /// <summary>
    /// Produces stable, readable section orientation from a detail/model line.
    /// Keeps section aligned with the selected line.
    /// Only flips the viewing direction when it points to an unwanted direction.
    /// </summary>
    public class SectionOrientationService
    {
        public OrientationResult Calculate(Line line)
        {
            if (line == null)
            {
                return new OrientationResult
                {
                    Success = false
                };
            }

            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);

            XYZ rawDir = p1 - p0;

            if (rawDir.GetLength() < 1e-9)
            {
                return new OrientationResult
                {
                    Success = false
                };
            }

            // XDir = section line direction.
            // This keeps the section perfectly aligned with the selected line.
            XYZ xDir = rawDir.Normalize();

            // YDir = vertical direction for section box.
            XYZ yDir = XYZ.BasisZ;

            // ZDir = viewing direction of the section.
            XYZ zDir = xDir.CrossProduct(yDir).Normalize();

            // Flip ONLY the viewing direction when it points to bad quadrants:
            // Down, Down-Left, Down-Right, or pure Left.
            bool isLookingDown = zDir.Y < -1e-9;
            bool isPureLeft = Math.Abs(zDir.Y) < 1e-9 && zDir.X < -1e-9;

            if (isLookingDown || isPureLeft)
            {
                xDir = xDir.Negate();
                zDir = zDir.Negate();
            }

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