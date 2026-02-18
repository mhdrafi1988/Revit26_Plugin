using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Models
{
    public class DrainItem
    {
        public XYZ CenterPoint { get; set; }
        public double Width { get; set; } // in mm
        public double Height { get; set; } // in mm
        public string SizeCategory => $"{Width:F0} x {Height:F0} mm";
        public string ShapeType { get; set; } = "Unknown";
        public bool IsSelected { get; set; } = true;
        public ElementId ElementId { get; set; }

        public List<XYZ> CornerPoints { get; set; }
        public int DrainId { get; set; }
        private static int _nextDrainId = 1;

        public DrainItem(XYZ center, double width, double height, ElementId id = null)
        {
            CenterPoint = center;
            Width = width;
            Height = height;
            ElementId = id;
            DrainId = _nextDrainId++;
            CornerPoints = CalculateCornerPoints();
        }

        public DrainItem(XYZ center, double width, double height, string shapeType, ElementId id = null)
        {
            CenterPoint = center;
            Width = width;
            Height = height;
            ShapeType = shapeType;
            ElementId = id;
            DrainId = _nextDrainId++;
            CornerPoints = CalculateCornerPoints();
        }

        private List<XYZ> CalculateCornerPoints()
        {
            var corners = new List<XYZ>();
            double halfWidth = (Width / 304.8) / 2;
            double halfHeight = (Height / 304.8) / 2;

            corners.Add(new XYZ(CenterPoint.X - halfWidth, CenterPoint.Y - halfHeight, CenterPoint.Z));
            corners.Add(new XYZ(CenterPoint.X + halfWidth, CenterPoint.Y - halfHeight, CenterPoint.Z));
            corners.Add(new XYZ(CenterPoint.X + halfWidth, CenterPoint.Y + halfHeight, CenterPoint.Z));
            corners.Add(new XYZ(CenterPoint.X - halfWidth, CenterPoint.Y + halfHeight, CenterPoint.Z));

            return corners;
        }

        public XYZ GetNearestCorner(XYZ point)
        {
            if (CornerPoints == null || CornerPoints.Count == 0)
                return CenterPoint;

            XYZ nearestCorner = CornerPoints[0];
            double minDistance = point.DistanceTo(nearestCorner);

            for (int i = 1; i < CornerPoints.Count; i++)
            {
                double distance = point.DistanceTo(CornerPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCorner = CornerPoints[i];
                }
            }
            return nearestCorner;
        }
    }
}