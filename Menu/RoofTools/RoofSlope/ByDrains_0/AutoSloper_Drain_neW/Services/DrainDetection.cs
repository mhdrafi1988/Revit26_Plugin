// =======================================================
// File: Services/Implementations/DrainDetectionService.cs
// Description: Drain detection service implementation
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Implementations
{
    public class DrainDetectionService : IDrainDetectionService
    {
        private readonly IUnitConversionService _unitService;

        public DrainDetectionService(IUnitConversionService unitService)
        {
            _unitService = unitService;
        }

        public List<DrainItem> DetectDrainsFromRoof(
            RoofBase roof,
            Face topFace,
            List<XYZ> vertices,
            double toleranceMm,
            bool enableTolerance)
        {
            var drains = new List<DrainItem>();

            if (topFace == null)
            {
                throw new ArgumentException("Top face cannot be null");
            }

            try
            {
                // Detect inner loops (openings) in the top face
                var edgeLoops = topFace.EdgeLoops;
                if (edgeLoops == null || edgeLoops.Size <= 1)
                {
                    return drains; // No inner loops found
                }

                // Loop 0 is outer boundary, inner loops start from index 1
                for (int i = 1; i < edgeLoops.Size; i++)
                {
                    var innerLoop = edgeLoops.get_Item(i);
                    var drain = CreateDrainFromLoop(innerLoop, topFace, toleranceMm, enableTolerance);
                    if (drain != null)
                    {
                        drains.Add(drain);
                    }
                }

                // Remove duplicates based on center point
                drains = RemoveDuplicateDrains(drains);
                drains = drains.OrderBy(d => d.Width * d.Height).ToList();

                return drains;
            }
            catch (Exception ex)
            {
                throw new Exception($"Drain detection failed: {ex.Message}", ex);
            }
        }

        private DrainItem CreateDrainFromLoop(EdgeArray loop, Face face, double toleranceMm, bool enableTolerance)
        {
            try
            {
                var points = new List<XYZ>();
                var curves = new List<Curve>();

                // Extract all points from the loop
                foreach (Edge edge in loop)
                {
                    var curve = edge.AsCurve();
                    if (curve == null) continue;

                    curves.Add(curve);
                    points.Add(curve.GetEndPoint(0));
                    points.Add(curve.GetEndPoint(1));
                }

                if (points.Count < 3) return null;

                // Project points to face if needed
                var projectedPoints = points.Select(p => ProjectToFace(face, p)).ToList();

                // Calculate bounding box dimensions (in feet, then convert to mm)
                double minX = projectedPoints.Min(p => p.X);
                double maxX = projectedPoints.Max(p => p.X);
                double minY = projectedPoints.Min(p => p.Y);
                double maxY = projectedPoints.Max(p => p.Y);

                double widthFeet = maxX - minX;
                double heightFeet = maxY - minY;

                double widthMm = _unitService.FeetToMm(widthFeet);
                double heightMm = _unitService.FeetToMm(heightFeet);

                // Validate size range (between 5mm and 2000mm)
                if (widthMm < 5 || heightMm < 5 || widthMm > 2000 || heightMm > 2000)
                    return null;

                // Calculate center point
                var centerX = (minX + maxX) / 2;
                var centerY = (minY + maxY) / 2;
                var centerZ = projectedPoints.Average(p => p.Z);
                var center = new XYZ(centerX, centerY, centerZ);

                // Determine shape type
                string shapeType = DetermineShapeType(curves, widthMm, heightMm);

                // Determine size category
                string sizeCategory = GetSizeCategory(widthMm, heightMm);

                // Create drain vertices (as Point3D in mm)
                var drainVertices = projectedPoints
                    .Select(p => _unitService.XyzToPoint3D(p))
                    .ToList();

                // Create drain item
                var drain = new DrainItem
                {
                    ShapeType = shapeType,
                    SizeCategory = sizeCategory,
                    Width = Math.Round(widthMm, 1),
                    Height = Math.Round(heightMm, 1),
                    CenterPoint = _unitService.XyzToPoint3D(center),
                    DrainVertices = drainVertices,
                    IsSelected = true,
                    ElementId = Guid.NewGuid().ToString()
                };

                return drain;
            }
            catch
            {
                return null;
            }
        }

        private string DetermineShapeType(List<Curve> curves, double widthMm, double heightMm)
        {
            // Check if it's a circle (has arcs)
            bool hasArcs = curves.Any(c => c is Arc);

            if (hasArcs)
            {
                // Check if it's approximately circular (width and height within 5% tolerance)
                double tolerance = Math.Max(widthMm, heightMm) * 0.05;
                if (Math.Abs(widthMm - heightMm) < tolerance)
                    return "Circle";
                return "Ellipse";
            }

            // Check if it's a square (width and height within 5mm tolerance)
            if (Math.Abs(widthMm - heightMm) < 5)
                return "Square";

            // Check if it's a rectangle (all curves are lines)
            if (curves.All(c => c is Line))
                return "Rectangle";

            // Default to polygon
            return "Polygon";
        }

        private string GetSizeCategory(double widthMm, double heightMm)
        {
            double maxDimension = Math.Max(widthMm, heightMm);

            if (maxDimension < 100)
                return "<100mm";
            else if (maxDimension < 200)
                return "100-200mm";
            else if (maxDimension < 300)
                return "200-300mm";
            else if (maxDimension < 400)
                return "300-400mm";
            else if (maxDimension < 500)
                return "400-500mm";
            else
                return ">500mm";
        }

        private XYZ ProjectToFace(Face face, XYZ point)
        {
            try
            {
                var result = face.Project(point);
                return result?.XYZPoint ?? point;
            }
            catch
            {
                return point;
            }
        }

        private List<DrainItem> RemoveDuplicateDrains(List<DrainItem> drains)
        {
            var unique = new List<DrainItem>();
            double toleranceFeet = 0.01; // ~3mm tolerance

            foreach (var drain in drains)
            {
                bool isDuplicate = unique.Any(existing =>
                    Math.Abs(existing.CenterPoint.X - drain.CenterPoint.X) < toleranceFeet &&
                    Math.Abs(existing.CenterPoint.Y - drain.CenterPoint.Y) < toleranceFeet);

                if (!isDuplicate)
                    unique.Add(drain);
            }

            return unique;
        }
    }
}