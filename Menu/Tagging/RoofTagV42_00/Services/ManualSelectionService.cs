using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26.RoofTagV42.Services
{
    public static class ManualSelectionService
    {
        public static List<XYZ> SelectManualPoints(UIDocument uiDocument, RoofBase roof)
        {
            var selectedPoints = new List<XYZ>();

            try
            {
                uiDocument.Application.ActiveUIDocument.Selection.SetElementIds(new List<ElementId>());

                while (true)
                {
                    try
                    {
                        // Pick point on roof
                        XYZ point = uiDocument.Selection.PickPoint(
                            ObjectSnapTypes.Endpoints | ObjectSnapTypes.Intersections | ObjectSnapTypes.Nearest,
                            "Select points on the roof surface (ESC to finish)");

                        // Project point to roof surface
                        if (ProjectToRoofSurface(roof, point, out XYZ projectedPoint))
                        {
                            selectedPoints.Add(projectedPoint);

                            // Show feedback
                            TaskDialog.Show("Point Selected",
                                $"Point {selectedPoints.Count} selected at:\n" +
                                $"X: {projectedPoint.X:F2}'\n" +
                                $"Y: {projectedPoint.Y:F2}'\n" +
                                $"Z: {projectedPoint.Z:F2}'");
                        }
                        else
                        {
                            TaskDialog.Show("Error", "Could not project point to roof surface. Try again.");
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // User pressed ESC
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Selection Error", $"Error selecting points: {ex.Message}");
            }

            return selectedPoints;
        }

        private static bool ProjectToRoofSurface(RoofBase roof, XYZ point, out XYZ projectedPoint)
        {
            projectedPoint = null;

            var options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            var geometry = roof.get_Geometry(options);
            if (geometry == null) return false;

            double minDistance = double.MaxValue;

            foreach (var geomObj in geometry)
            {
                if (geomObj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        var projection = face.Project(point);
                        if (projection == null) continue;

                        var normal = face.ComputeNormal(projection.UVPoint);
                        if (Math.Abs(normal.Z) < 0.2) continue;

                        double distance = point.DistanceTo(projection.XYZPoint);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            projectedPoint = projection.XYZPoint;
                        }
                    }
                }
            }

            return projectedPoint != null;
        }

        public static List<XYZ> GetHighLowPoints(RoofBase roof, int numberOfPoints = 4)
        {
            var points = new List<XYZ>();

            // Get roof vertices
            var vertices = GeometryService.GetRoofVertices(roof);
            if (vertices.Count == 0) return points;

            // Sort by elevation (Z coordinate)
            var sortedByElevation = vertices.OrderBy(v => v.Z).ToList();

            // Add lowest points
            int halfCount = Math.Min(numberOfPoints / 2, sortedByElevation.Count);
            for (int i = 0; i < halfCount; i++)
            {
                points.Add(sortedByElevation[i]);
            }

            // Add highest points
            for (int i = Math.Max(0, sortedByElevation.Count - halfCount); i < sortedByElevation.Count; i++)
            {
                if (points.Count >= numberOfPoints) break;
                points.Add(sortedByElevation[i]);
            }

            return points;
        }
    }
}