using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Asd_19.Services
{
    public class DrainItem
    {
        public XYZ CenterPoint { get; set; } // Keep for backward compatibility
        public double Width { get; set; } // in mm
        public double Height { get; set; } // in mm
        public string SizeCategory => $"{Width:F0} x {Height:F0} mm";
        public string ShapeType { get; set; } = "Unknown";
        public bool IsSelected { get; set; } = true;
        public ElementId ElementId { get; set; }

        // NEW: Store the actual shape editing vertices that belong to this drain
        public List<SlabShapeVertex> DrainVertices { get; set; }

        // NEW: Store the loop curves for accurate distance checking
        public List<Curve> LoopCurves { get; set; }

        // Store corner points for accurate distance calculation
        public List<XYZ> CornerPoints { get; set; }

        // Track which drain this belongs to for pathfinding
        public int DrainId { get; set; }
        private static int _nextDrainId = 1;

        // Constructor with drain vertices
        public DrainItem(List<SlabShapeVertex> drainVertices, List<Curve> loopCurves,
                        double width, double height, string shapeType, ElementId id = null)
        {
            DrainVertices = drainVertices ?? new List<SlabShapeVertex>();
            LoopCurves = loopCurves ?? new List<Curve>();
            Width = width;
            Height = height;
            ShapeType = shapeType;
            ElementId = id;
            DrainId = _nextDrainId++;

            // Calculate center point as average of all drain vertices
            if (drainVertices != null && drainVertices.Count > 0)
            {
                CenterPoint = new XYZ(
                    drainVertices.Average(v => v.Position.X),
                    drainVertices.Average(v => v.Position.Y),
                    drainVertices.Average(v => v.Position.Z)
                );
            }

            CornerPoints = CalculateCornerPoints();
        }

        // Legacy constructor for backward compatibility
        public DrainItem(XYZ center, double width, double height, ElementId id = null)
        {
            CenterPoint = center;
            Width = width;
            Height = height;
            ElementId = id;
            DrainId = _nextDrainId++;
            DrainVertices = new List<SlabShapeVertex>();
            LoopCurves = new List<Curve>();
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
            DrainVertices = new List<SlabShapeVertex>();
            LoopCurves = new List<Curve>();
            CornerPoints = CalculateCornerPoints();
        }

        private List<XYZ> CalculateCornerPoints()
        {
            var corners = new List<XYZ>();

            // Convert dimensions from mm to feet for Revit coordinates
            double halfWidth = (Width / 304.8) / 2;
            double halfHeight = (Height / 304.8) / 2;

            // Calculate four corner points around the center
            corners.Add(new XYZ(CenterPoint.X - halfWidth, CenterPoint.Y - halfHeight, CenterPoint.Z)); // Bottom-left
            corners.Add(new XYZ(CenterPoint.X + halfWidth, CenterPoint.Y - halfHeight, CenterPoint.Z)); // Bottom-right
            corners.Add(new XYZ(CenterPoint.X + halfWidth, CenterPoint.Y + halfHeight, CenterPoint.Z)); // Top-right
            corners.Add(new XYZ(CenterPoint.X - halfWidth, CenterPoint.Y + halfHeight, CenterPoint.Z)); // Top-left

            return corners;
        }

        public XYZ GetNearestCorner(XYZ point)
        {
            if (CornerPoints == null || CornerPoints.Count == 0)
                return CenterPoint; // Fallback to center if no corners

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

        // NEW: Check if a vertex belongs to this drain (within 5mm tolerance)
        public bool ContainsVertex(SlabShapeVertex vertex, double toleranceMm = 5.0)
        {
            if (vertex?.Position == null || LoopCurves == null) return false;

            double toleranceFeet = toleranceMm / 304.8;

            foreach (var curve in LoopCurves)
            {
                try
                {
                    double distance = curve.Distance(vertex.Position);
                    if (distance < toleranceFeet)
                        return true;
                }
                catch
                {
                    // Skip problematic curves
                }
            }

            return false;
        }
    }
}