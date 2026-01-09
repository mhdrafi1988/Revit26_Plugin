using Autodesk.Revit.DB;
using System;

namespace Revit22_Plugin.PlanSections.Services
{
    /// <summary>
    /// Ensures section always faces upward/right with stable orientation.
    /// </summary>
    public class SectionOrientationService
    {
        public class OrientationResult
        {
            public XYZ XDir { get; set; }
            public XYZ YDir { get; set; }
            public XYZ ZDir { get; set; }

            public XYZ StartPoint { get; set; }
            public XYZ EndPoint { get; set; }

            public bool Success { get; set; }
        }

        public OrientationResult CalculateOrientation(Line line)
        {
            try
            {
                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);

                XYZ startPoint;
                XYZ endPoint;

                // Determine natural direction
                XYZ dir = (p1 - p0).Normalize();

                bool moreHorizontal = Math.Abs(dir.X) >= Math.Abs(dir.Y);

                // Sort endpoints for consistency
                if (moreHorizontal)
                {
                    // left → right
                    if (p0.X < p1.X)
                    {
                        startPoint = p0;
                        endPoint = p1;
                    }
                    else
                    {
                        startPoint = p1;
                        endPoint = p0;
                    }
                }
                else
                {
                    // bottom → top (Y)
                    if (p0.Y < p1.Y)
                    {
                        startPoint = p0;
                        endPoint = p1;
                    }
                    else
                    {
                        startPoint = p1;
                        endPoint = p0;
                    }
                }

                // Recompute normalized
                dir = (endPoint - startPoint).Normalize();

                // Perpendicular 2D (rotate 90° CCW)
                XYZ zDir = new XYZ(-dir.Y, dir.X, 0).Normalize();

                // Force "positive" direction (up/right)
                if (zDir.X < 0 && zDir.Y < 0)
                    zDir = zDir.Negate();
                else if (Math.Abs(zDir.X) < 1e-9 && zDir.Y < 0)
                    zDir = zDir.Negate();
                else if (zDir.X < 0 && Math.Abs(zDir.Y) < 1e-9)
                    zDir = zDir.Negate();

                XYZ yDir = XYZ.BasisZ;                 // Up
                XYZ xDir = yDir.CrossProduct(zDir).Normalize();

                return new OrientationResult
                {
                    XDir = xDir,
                    YDir = yDir,
                    ZDir = zDir,
                    StartPoint = startPoint,
                    EndPoint = endPoint,
                    Success = true
                };
            }
            catch
            {
                return new OrientationResult
                {
                    XDir = XYZ.BasisX,
                    YDir = XYZ.BasisZ,
                    ZDir = XYZ.BasisY,
                    StartPoint = line.GetEndPoint(0),
                    EndPoint = line.GetEndPoint(1),
                    Success = false
                };
            }
        }
    }
}
