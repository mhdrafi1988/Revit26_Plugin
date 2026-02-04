using Autodesk.Revit.DB;
using RoofTagV3.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;

namespace RoofTagV3.Services
{
    public static class TaggingService
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "RoofTagV3_Execution.log");

        public static bool PlaceRoofSpotElevation(
            Document document,
            RoofBase roof,
            XYZ point,
            XYZ centroid,
            List<XYZ> boundary,
            RoofTagViewModel viewModel)
        {
            if (document == null || roof == null || point == null || viewModel == null)
                return false;

            var view = document.ActiveView;
            if (view == null) return false;

            try
            {
                // Get reference on roof face
                if (!GetFaceReference(roof, point, out Reference faceReference, out XYZ projectedPoint))
                    return false;

                // Calculate leader points
                XYZ bendPoint = CalculateBendPoint(projectedPoint, centroid,
                    viewModel.BendOffsetFeet, viewModel.BendInward);

                XYZ endPoint = CalculateEndPoint(projectedPoint, bendPoint,
                    viewModel.SelectedAngle, viewModel.EndOffsetFeet,
                    viewModel.BendInward);

                // Adjust for boundary collisions
                endPoint = AdjustEndPointForBoundary(bendPoint, endPoint, boundary);

                // Create spot elevation
                SpotDimension spotElevation = document.Create.NewSpotElevation(
                    view,
                    faceReference,
                    projectedPoint,
                    bendPoint,
                    endPoint,
                    projectedPoint,
                    viewModel.UseLeader);

                if (spotElevation != null && viewModel.SelectedSpotTagType != null)
                {
                    spotElevation.ChangeTypeId(viewModel.SelectedSpotTagType.Id);
                    LogToFile($"Successfully placed tag at {FormatPoint(projectedPoint)}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to place tag at {FormatPoint(point)}: {ex.Message}");
            }

            return false;
        }

        private static bool GetFaceReference(Element element, XYZ point, out Reference reference, out XYZ projectedPoint)
        {
            reference = null;
            projectedPoint = null;

            var options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            var geometry = element.get_Geometry(options);
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
                        if (Math.Abs(normal.Z) < 0.2) continue; // Skip near-vertical faces

                        double distance = point.DistanceTo(projection.XYZPoint);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            reference = face.Reference;
                            projectedPoint = projection.XYZPoint;
                        }
                    }
                }
            }

            return reference != null;
        }

        private static XYZ CalculateBendPoint(XYZ origin, XYZ centroid, double offset, bool inward)
        {
            XYZ direction = (centroid - origin).Normalize();
            if (!inward) direction = -direction;
            return origin + direction * offset;
        }

        private static XYZ CalculateEndPoint(XYZ origin, XYZ bend, double angleDegrees, double offset, bool inward)
        {
            double angleRadians = angleDegrees * Math.PI / 180.0;
            XYZ baseDirection = (bend - origin).Normalize();

            // Rotate the direction
            double cos = Math.Cos(angleRadians);
            double sin = Math.Sin(angleRadians);

            XYZ rotated = new XYZ(
                baseDirection.X * cos - baseDirection.Y * sin,
                baseDirection.X * sin + baseDirection.Y * cos,
                0);

            if (!inward) rotated = -rotated;

            return bend + rotated.Normalize() * offset;
        }

        private static XYZ AdjustEndPointForBoundary(XYZ bend, XYZ end, List<XYZ> boundary)
        {
            // Simple collision avoidance - extend if near boundary
            const double safetyMargin = 2.0; // feet
            foreach (var boundaryPoint in boundary)
            {
                if (end.DistanceTo(boundaryPoint) < safetyMargin)
                {
                    XYZ direction = (end - bend).Normalize();
                    return bend + direction * (end.DistanceTo(bend) + safetyMargin);
                }
            }
            return end;
        }

        private static void LogToFile(string message)
        {
            try
            {
                File.AppendAllText(LogFilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
            }
            catch
            {
                // Silent fail for logging
            }
        }

        private static string FormatPoint(XYZ point)
        {
            return $"({point.X:F2}, {point.Y:F2}, {point.Z:F2})";
        }
    }
}