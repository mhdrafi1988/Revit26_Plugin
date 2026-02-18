using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Models
{
    public class DrainItem
    {
        private static int _nextId = 1;

        public int DrainId { get; }
        public XYZ CenterPoint { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string ShapeType { get; set; } = "Unknown";
        public bool IsSelected { get; set; } = true;
        public List<XYZ> CornerPoints { get; set; }

        public string SizeCategory => $"{Width:F0} x {Height:F0} mm";

        public DrainItem(XYZ center, double width, double height, string shapeType = "Unknown")
        {
            DrainId = _nextId++;
            CenterPoint = center;
            Width = width;
            Height = height;
            ShapeType = shapeType;
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

            XYZ nearest = CornerPoints[0];
            double minDist = point.DistanceTo(nearest);

            for (int i = 1; i < CornerPoints.Count; i++)
            {
                double dist = point.DistanceTo(CornerPoints[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = CornerPoints[i];
                }
            }
            return nearest;
        }
    }
}