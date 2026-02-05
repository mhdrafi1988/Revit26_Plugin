using Autodesk.Revit.DB;
using Revit26.RoofTagV42.Models;
using Revit26.RoofTagV42.ViewModels;
using Revit26.RoofTagV42.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revit26.RoofTagV42.Services
{
    public class TaggingService : ITaggingService
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Revit26_RoofTagV42_Execution.log");

        // Professional standards constants
        private const double MIN_LEADER_LENGTH = 1.0; // 1 foot minimum
        private const double MIN_TAG_SPACING = 2.0; // 2 feet between tags
        private const double LEADER_CLEARANCE = 0.5; // 0.5 feet from edges
        private const double STANDARD_ANGLE = 45.0; // Standard 45° angle
        private const double BOUNDARY_MARGIN = 2.0; // 2 feet from boundary
        private const double MAX_LEADER_LENGTH = 20.0; // 20 feet maximum

        public TaggingResult PlaceRoofSpotElevation(
            Document document,
            RoofBase roof,
            XYZ point,
            XYZ centroid,
            List<XYZ> boundary,
            RoofTagViewModel viewModel,
            RoofTagWindow window = null,
            List<XYZ> existingTagPoints = null)
        {
            // Parameter validation
            if (document == null || roof == null || point == null || viewModel == null)
            {
                LogToWindow("ERROR: Invalid input parameters", window);
                return TaggingResult.FailureResult("Invalid input parameters");
            }

            var view = document.ActiveView;
            if (view == null)
            {
                LogToWindow("ERROR: No active view found", window);
                return TaggingResult.FailureResult("No active view found");
            }

            // Check if point is too close to boundary
            if (IsTooCloseToBoundary(point, boundary))
            {
                LogToWindow($"WARNING: Point too close to roof boundary", window);
                // Continue anyway, but log warning
            }

            try
            {
                LogToWindow($"Processing point at ({point.X:F2}, {point.Y:F2}, {point.Z:F2})", window);

                // Get face reference - FIXED: Declare variables first
                Reference faceReference;
                XYZ projectedPoint;
                if (!GetFaceReference(roof, point, out faceReference, out projectedPoint))
                {
                    LogToWindow("ERROR: Could not find face reference for point", window);
                    return TaggingResult.FailureResult("Could not find face reference");
                }

                LogToWindow($"Projected to surface: ({projectedPoint.X:F2}, {projectedPoint.Y:F2}, {projectedPoint.Z:F2})", window);

                // Check minimum spacing from existing tags
                if (existingTagPoints != null && existingTagPoints.Any())
                {
                    double minDistance = existingTagPoints.Min(p => p.DistanceTo(projectedPoint));
                    if (minDistance < MIN_TAG_SPACING)
                    {
                        LogToWindow($"WARNING: Point too close to existing tag ({minDistance:F2} ft < {MIN_TAG_SPACING} ft)", window);
                        return TaggingResult.FailureResult($"Tag spacing too small. Minimum spacing: {MIN_TAG_SPACING} ft");
                    }
                }

                // Create spot elevation
                SpotDimension spotElevation = CreateSpotElevation(
                    document, view, faceReference, projectedPoint, centroid, boundary, viewModel, existingTagPoints);

                if (spotElevation != null)
                {
                    ApplyTagType(spotElevation, viewModel, window);

                    // Apply professional formatting if available
                    ApplyProfessionalFormatting(spotElevation, document);

                    LogToFile($"Successfully placed tag at {FormatPoint(projectedPoint)}");

                    return new TaggingResult
                    {
                        Success = true,
                        Message = "Tag placed successfully",
                        TagsPlaced = 1
                    };
                }

                LogToWindow("Failed to create spot elevation", window);
                return TaggingResult.FailureResult("Failed to create spot elevation");
            }
            catch (Exception ex)
            {
                LogToWindow($"ERROR: {ex.Message}", window);
                LogToFile($"Failed to place tag at {FormatPoint(point)}: {ex.Message}");
                return TaggingResult.FailureResult(ex.Message);
            }
        }

        private SpotDimension CreateSpotElevation(
            Document document,
            View view,
            Reference faceReference,
            XYZ projectedPoint,
            XYZ centroid,
            List<XYZ> boundary,
            RoofTagViewModel viewModel,
            List<XYZ> existingTagPoints = null)
        {
            if (!viewModel.UseLeader)
            {
                // Place tag without leader (on surface)
                return document.Create.NewSpotElevation(
                    view, faceReference, projectedPoint,
                    projectedPoint, projectedPoint, projectedPoint, false);
            }

            // Calculate optimal bend direction based on professional standards
            bool shouldBendInward = DetermineOptimalBendDirection(projectedPoint, centroid, boundary, viewModel.BendInward);

            // Calculate leader points with professional constraints
            double bendOffset = ApplyProfessionalConstraints(viewModel.BendOffsetFeet, MIN_LEADER_LENGTH, MAX_LEADER_LENGTH);
            double endOffset = ApplyProfessionalConstraints(viewModel.EndOffsetFeet, MIN_LEADER_LENGTH, MAX_LEADER_LENGTH);

            XYZ bendPoint = CalculateBendPoint(projectedPoint, centroid, bendOffset, shouldBendInward, boundary);

            // Ensure bend point is not too close to boundary
            bendPoint = AdjustPointForBoundary(bendPoint, boundary);

            XYZ endPoint = CalculateEndPoint(projectedPoint, bendPoint,
                viewModel.SelectedAngle, endOffset, shouldBendInward, centroid, boundary, existingTagPoints);

            // Ensure end point follows professional standards
            endPoint = ApplyEndPointStandards(endPoint, bendPoint, boundary, view);

            // Create the spot elevation with leader
            return document.Create.NewSpotElevation(
                view, faceReference, projectedPoint,
                bendPoint, endPoint, projectedPoint, true);
        }

        private bool DetermineOptimalBendDirection(XYZ point, XYZ centroid, List<XYZ> boundary, bool userPreference)
        {
            // Respect user preference if explicitly set
            if (!userPreference) return false;

            // Calculate distance to nearest boundary
            if (boundary != null && boundary.Count > 2)
            {
                double minDistanceToBoundary = boundary.Min(b => b.DistanceTo(point));

                // If point is close to boundary (< 5 feet), bend outward for better visibility
                if (minDistanceToBoundary < 5.0)
                {
                    return false; // Bend outward
                }
            }

            // Calculate position relative to centroid
            XYZ directionToCentroid = (centroid - point).Normalize();

            // For complex roofs, bend inward for interior points, outward for edge points
            if (IsInteriorPoint(point, boundary))
            {
                return true; // Bend inward for interior points
            }

            return false; // Bend outward for edge points
        }

        private bool IsInteriorPoint(XYZ point, List<XYZ> boundary)
        {
            if (boundary == null || boundary.Count < 3) return false;

            // Simple check: if point is far from all boundary points
            double avgDistance = boundary.Average(b => b.DistanceTo(point));
            return avgDistance > 10.0; // More than 10 feet from boundary average
        }

        private bool IsTooCloseToBoundary(XYZ point, List<XYZ> boundary)
        {
            if (boundary == null || boundary.Count == 0) return false;

            double minDistance = boundary.Min(b => b.DistanceTo(point));
            return minDistance < BOUNDARY_MARGIN;
        }

        private XYZ CalculateBendPoint(XYZ origin, XYZ centroid, double offset, bool inward, List<XYZ> boundary)
        {
            if (offset <= 0) return origin;

            XYZ direction = (centroid - origin).Normalize();

            // If direction is too shallow, adjust for better visibility
            if (Math.Abs(direction.Z) > 0.9) // Mostly vertical
            {
                // Use horizontal direction instead
                direction = new XYZ(direction.X, direction.Y, 0).Normalize();
            }

            if (!inward) direction = -direction;

            XYZ bendPoint = origin + direction * offset;

            // Ensure bend point maintains minimum distance from boundary
            return AdjustPointForBoundary(bendPoint, boundary);
        }

        private XYZ CalculateEndPoint(XYZ origin, XYZ bend, double angleDegrees, double offset,
                                     bool bendInward, XYZ centroid, List<XYZ> boundary, List<XYZ> existingTagPoints)
        {
            if (offset <= MIN_LEADER_LENGTH) return bend;

            // Get quadrant relative to centroid
            string quadrant = GetQuadrant(origin, centroid);

            // Get base direction based on quadrant and bend direction
            XYZ baseDirection = GetQuadrantDirection(quadrant, bendInward);

            // Apply professional angle standards
            double effectiveAngle = ApplyAngleStandards(angleDegrees);

            // Rotate from standard 45° if needed
            if (Math.Abs(effectiveAngle - STANDARD_ANGLE) > 0.001)
            {
                double angleOffset = (effectiveAngle - STANDARD_ANGLE) * Math.PI / 180.0;
                double cos = Math.Cos(angleOffset);
                double sin = Math.Sin(angleOffset);

                baseDirection = new XYZ(
                    baseDirection.X * cos - baseDirection.Y * sin,
                    baseDirection.X * sin + baseDirection.Y * cos,
                    0);
            }

            baseDirection = baseDirection.Normalize();

            // Adjust to avoid collisions with existing tags
            baseDirection = AvoidTagCollisions(bend, baseDirection, existingTagPoints, offset);

            // Adjust to avoid boundary intersections
            baseDirection = AvoidBoundaryIntersections(bend, baseDirection, boundary, offset);

            XYZ endPoint = bend + baseDirection * offset;

            // Ensure end point is within reasonable bounds
            return ClampEndPoint(endPoint, bend, offset);
        }

        private string GetQuadrant(XYZ point, XYZ centroid)
        {
            double dx = point.X - centroid.X;
            double dy = point.Y - centroid.Y;

            const double tolerance = 0.001;

            // Prioritize larger displacement for better angle selection
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                if (dx > tolerance) return dy >= -tolerance ? "TopRight" : "BottomRight";
                if (dx < -tolerance) return dy >= -tolerance ? "TopLeft" : "BottomLeft";
            }
            else
            {
                if (dy > tolerance) return dx >= -tolerance ? "TopRight" : "TopLeft";
                if (dy < -tolerance) return dx >= -tolerance ? "BottomRight" : "BottomLeft";
            }

            // Default to TopRight if near centroid
            return "TopRight";
        }

        private XYZ GetQuadrantDirection(string quadrant, bool bendInward)
        {
            // Professional standard: Leaders point away from text at consistent angles
            switch (quadrant)
            {
                case "TopRight":
                    return bendInward ? new XYZ(-0.707, -0.707, 0) : new XYZ(0.707, 0.707, 0); // 45° SW/NE

                case "TopLeft":
                    return bendInward ? new XYZ(0.707, -0.707, 0) : new XYZ(-0.707, 0.707, 0); // 45° SE/NW

                case "BottomRight":
                    return bendInward ? new XYZ(-0.707, 0.707, 0) : new XYZ(0.707, -0.707, 0); // 45° NW/SE

                case "BottomLeft":
                    return bendInward ? new XYZ(0.707, 0.707, 0) : new XYZ(-0.707, -0.707, 0); // 45° NE/SW

                default:
                    return new XYZ(1, 0, 0).Normalize(); // Default horizontal
            }
        }

        private XYZ AvoidTagCollisions(XYZ start, XYZ direction, List<XYZ> existingTagPoints, double offset)
        {
            if (existingTagPoints == null || !existingTagPoints.Any()) return direction;

            XYZ testPoint = start + direction * offset;

            foreach (var tagPoint in existingTagPoints)
            {
                double distance = testPoint.DistanceTo(tagPoint);
                if (distance < MIN_TAG_SPACING)
                {
                    // Rotate direction by 30° to avoid collision
                    double rotateAngle = 30.0 * Math.PI / 180.0;
                    double cos = Math.Cos(rotateAngle);
                    double sin = Math.Sin(rotateAngle);

                    return new XYZ(
                        direction.X * cos - direction.Y * sin,
                        direction.X * sin + direction.Y * cos,
                        0).Normalize();
                }
            }

            return direction;
        }

        private XYZ AvoidBoundaryIntersections(XYZ start, XYZ direction, List<XYZ> boundary, double offset)
        {
            if (boundary == null || boundary.Count < 2) return direction;

            // Create test line
            XYZ endPoint = start + direction * offset;
            Line testLine = Line.CreateBound(start, endPoint);

            // Check intersection with boundary segments
            for (int i = 0; i < boundary.Count; i++)
            {
                XYZ point1 = boundary[i];
                XYZ point2 = boundary[(i + 1) % boundary.Count];

                Line boundaryLine = Line.CreateBound(point1, point2);

                // Fix: Use the correct overload without 'out'
                if (testLine.Intersect(boundaryLine) == SetComparisonResult.Overlap)
                {
                    // Intersection found, rotate direction
                    double rotateAngle = 45.0 * Math.PI / 180.0;
                    double cos = Math.Cos(rotateAngle);
                    double sin = Math.Sin(rotateAngle);

                    return new XYZ(
                        direction.X * cos - direction.Y * sin,
                        direction.X * sin + direction.Y * cos,
                        0).Normalize();
                }
            }

            return direction;
        }

        private XYZ AdjustPointForBoundary(XYZ point, List<XYZ> boundary)
        {
            if (boundary == null || boundary.Count == 0) return point;

            // Find nearest boundary point
            double minDistance = double.MaxValue;
            XYZ nearestBoundary = point;

            foreach (var boundaryPoint in boundary)
            {
                double distance = point.DistanceTo(boundaryPoint);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestBoundary = boundaryPoint;
                }
            }

            // If too close to boundary, move away
            if (minDistance < LEADER_CLEARANCE)
            {
                XYZ direction = (point - nearestBoundary).Normalize();
                return point + direction * (LEADER_CLEARANCE - minDistance + 0.1);
            }

            return point;
        }

        private XYZ ApplyEndPointStandards(XYZ endPoint, XYZ bendPoint, List<XYZ> boundary, View view)
        {
            // Ensure minimum leader length
            double leaderLength = endPoint.DistanceTo(bendPoint);
            if (leaderLength < MIN_LEADER_LENGTH)
            {
                XYZ direction = (endPoint - bendPoint).Normalize();
                endPoint = bendPoint + direction * MIN_LEADER_LENGTH;
            }

            // Ensure maximum leader length
            if (leaderLength > MAX_LEADER_LENGTH)
            {
                XYZ direction = (endPoint - bendPoint).Normalize();
                endPoint = bendPoint + direction * MAX_LEADER_LENGTH;
            }

            // Adjust for view crop box if available
            BoundingBoxXYZ cropBox = view.CropBox;
            if (cropBox != null && !cropBox.Min.IsZeroLength() && !cropBox.Max.IsZeroLength())
            {
                double margin = 1.0; // 1 foot margin
                double x = Math.Max(cropBox.Min.X + margin, Math.Min(cropBox.Max.X - margin, endPoint.X));
                double y = Math.Max(cropBox.Min.Y + margin, Math.Min(cropBox.Max.Y - margin, endPoint.Y));

                // Maintain Z coordinate
                endPoint = new XYZ(x, y, endPoint.Z);
            }

            return endPoint;
        }

        private XYZ ClampEndPoint(XYZ endPoint, XYZ bendPoint, double maxOffset)
        {
            double distance = endPoint.DistanceTo(bendPoint);
            if (distance > maxOffset)
            {
                XYZ direction = (endPoint - bendPoint).Normalize();
                return bendPoint + direction * maxOffset;
            }

            return endPoint;
        }

        private double ApplyProfessionalConstraints(double offset, double min, double max)
        {
            if (offset < min) return min;
            if (offset > max) return max;
            return offset;
        }

        private double ApplyAngleStandards(double angle)
        {
            // Round to nearest 15° for professional consistency
            double rounded = Math.Round(angle / 15.0) * 15.0;
            return Math.Max(0, Math.Min(180, rounded));
        }

        private bool GetFaceReference(Element element, XYZ point, out Reference reference, out XYZ projectedPoint)
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
                        // Look for faces that are relatively horizontal
                        if (Math.Abs(normal.Z) < 0.5) continue;

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

        private void ApplyTagType(SpotDimension spotElevation, RoofTagViewModel viewModel, RoofTagWindow window)
        {
            if (viewModel.SelectedSpotTagType != null)
            {
                try
                {
                    spotElevation.ChangeTypeId(viewModel.SelectedSpotTagType.Id);
                    LogToWindow($"? Tag placed successfully (Type: {viewModel.SelectedSpotTagType.Name})", window);
                }
                catch (Exception ex)
                {
                    LogToWindow($"? Could not apply tag type: {ex.Message}", window);
                }
            }
            else
            {
                LogToWindow("? Tag placed (using default type)", window);
            }
        }

        private void ApplyProfessionalFormatting(SpotDimension spotElevation, Document document)
        {
            try
            {
                // Try to set precision using different parameter names
                string[] precisionParamNames = {
                    "Spot Elevation Precision",
                    "Elevation Precision",
                    "Precision",
                    "Text Precision"
                };

                foreach (var paramName in precisionParamNames)
                {
                    Parameter precisionParam = spotElevation.LookupParameter(paramName);
                    if (precisionParam != null && !precisionParam.IsReadOnly)
                    {
                        try
                        {
                            if (precisionParam.StorageType == StorageType.Integer)
                            {
                                // 2 = 0.01 precision, 3 = 0.001 precision
                                precisionParam.Set(2);
                                break;
                            }
                            else if (precisionParam.StorageType == StorageType.Double)
                            {
                                precisionParam.Set(0.01);
                                break;
                            }
                        }
                        catch
                        {
                            // Try next parameter
                        }
                    }
                }

                // For Revit 2016+, use DisplayUnitType instead
                try
                {
                    // Get the spot dimension type
                    ElementId typeId = spotElevation.GetTypeId();
                    SpotDimensionType spotType = document.GetElement(typeId) as SpotDimensionType;

                    if (spotType != null)
                    {
                        // Set precision via type parameters
                        Parameter typePrecision = spotType.LookupParameter("Spot Elevation Precision");
                        if (typePrecision != null && !typePrecision.IsReadOnly)
                        {
                            typePrecision.Set(2); // 0.01 precision
                        }

                        // Set units format
                        Parameter typeUnits = spotType.LookupParameter("Unit Format");
                        if (typeUnits != null && !typeUnits.IsReadOnly)
                        {
                            // Get project units for consistency
                            Units projectUnits = document.GetUnits();
                            FormatOptions formatOptions = projectUnits.GetFormatOptions(SpecTypeId.Length);

                            if (formatOptions != null)
                            {
                                formatOptions.Accuracy = 0.01;
                                projectUnits.SetFormatOptions(SpecTypeId.Length, formatOptions);
                            }
                        }
                    }
                }
                catch
                {
                    // Older Revit version or different API
                }

                // Try to set rounding
                Parameter roundingParam = spotElevation.LookupParameter("Rounding");
                if (roundingParam != null && !roundingParam.IsReadOnly)
                {
                    if (roundingParam.StorageType == StorageType.Integer)
                    {
                        roundingParam.Set(1); // 0.01 rounding
                    }
                }
            }
            catch
            {
                // Silent fail - formatting is not critical
            }
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

        public static void LogToWindow(string message, RoofTagWindow window = null)
        {
            LogToFile(message);

            if (window != null)
            {
                try
                {
                    window.UpdateLog(message);
                }
                catch
                {
                    // Silent fail for UI updates
                }
            }
        }

        private static string FormatPoint(XYZ point)
        {
            return $"({point.X:F2}, {point.Y:F2}, {point.Z:F2})";
        }
    }

    public interface ITaggingService
    {
        TaggingResult PlaceRoofSpotElevation(
            Document document,
            RoofBase roof,
            XYZ point,
            XYZ centroid,
            List<XYZ> boundary,
            RoofTagViewModel viewModel,
            RoofTagWindow window = null,
            List<XYZ> existingTagPoints = null);
    }
}