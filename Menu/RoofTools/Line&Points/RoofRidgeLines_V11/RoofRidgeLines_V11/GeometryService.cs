using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Services
{
    public static class GeometryService
    {
        public static DetailLine CreateDetailLine(Document doc, View view, XYZ p1, XYZ p2)
        {
            Line ln = Line.CreateBound(p1, p2);
            DetailLine detailLine = doc.Create.NewDetailCurve(view, ln) as DetailLine;

            if (detailLine != null)
            {
                SetLineGraphics(doc, detailLine);
            }

            return detailLine;
        }

        private static void SetLineGraphics(Document doc, DetailLine detailLine)
        {
            // Create or get existing override settings
            OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();

            // Set color to Red (RGB: 255, 0, 0)
            overrideSettings.SetProjectionLineColor(new Color(255, 0, 0));

            // Set line weight to 5
            overrideSettings.SetProjectionLineWeight(5);

            // Apply the override to the detail line in the active view
            doc.ActiveView.SetElementOverrides(detailLine.Id, overrideSettings);
        }

        public static List<DetailLine> CreatePerpendicularLines(
            Document doc, View view, RoofBase roof, XYZ p1, XYZ p2)
        {
            var result = new List<DetailLine>();
            XYZ mid = (p1 + p2) / 2;
            XYZ dir = (p2 - p1).Normalize();
            XYZ perp = new XYZ(-dir.Y, dir.X, 0);

            // Try to get roof bounds first
            BoundingBoxXYZ bbox = roof?.get_BoundingBox(view);
            double roofSize = bbox != null ?
                Math.Max(bbox.Max.X - bbox.Min.X, bbox.Max.Y - bbox.Min.Y) :
                100; // fallback size in feet (approx 30m)

            double r = roofSize * 1.5;
            Line ray = Line.CreateBound(mid - perp * r, mid + perp * r);

            // Try to find intersection with roof boundaries
            var roofEdges = GetRoofEdges(roof).ToList();
            var leftHits = new List<XYZ>();
            var rightHits = new List<XYZ>();

            foreach (Curve c in roofEdges)
            {
                if (c == null) continue;

                IntersectionResultArray arr;
                SetComparisonResult compare = c.Intersect(ray, out arr);

                if (compare == SetComparisonResult.Overlap && arr != null && arr.Size > 0)
                {
                    foreach (IntersectionResult ir in arr)
                    {
                        XYZ hit = ir.XYZPoint;

                        // Classify left or right side
                        double dot = (hit - mid).DotProduct(perp);
                        if (dot > 0)
                            rightHits.Add(hit);
                        else
                            leftHits.Add(hit);
                    }
                }
            }

            // FALLBACK: If no intersections found or only one side found, create fixed length lines
            double fixedLength = UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Meters); // 3 meters

            // Handle left side
            if (leftHits.Any())
            {
                var closestLeft = leftHits.OrderBy(p => p.DistanceTo(mid)).First();
                result.Add(CreateDetailLine(doc, view, mid, closestLeft));
            }
            else
            {
                // Create fixed length line on left side
                XYZ leftPoint = mid - perp * fixedLength;
                result.Add(CreateDetailLine(doc, view, mid, leftPoint));
            }

            // Handle right side
            if (rightHits.Any())
            {
                var closestRight = rightHits.OrderBy(p => p.DistanceTo(mid)).First();
                result.Add(CreateDetailLine(doc, view, mid, closestRight));
            }
            else
            {
                // Create fixed length line on right side
                XYZ rightPoint = mid + perp * fixedLength;
                result.Add(CreateDetailLine(doc, view, mid, rightPoint));
            }

            return result;
        }

        // NEW: Overload that always uses fixed length (for testing or specific use cases)
        public static List<DetailLine> CreateFixedLengthPerpendicularLines(
            Document doc, View view, XYZ p1, XYZ p2, double lengthInMeters = 3)
        {
            var result = new List<DetailLine>();
            XYZ mid = (p1 + p2) / 2;
            XYZ dir = (p2 - p1).Normalize();
            XYZ perp = new XYZ(-dir.Y, dir.X, 0);

            double length = UnitUtils.ConvertToInternalUnits(lengthInMeters, UnitTypeId.Meters);

            // Create both perpendicular lines at fixed length
            XYZ leftPoint = mid - perp * length;
            XYZ rightPoint = mid + perp * length;

            result.Add(CreateDetailLine(doc, view, mid, leftPoint));
            result.Add(CreateDetailLine(doc, view, mid, rightPoint));

            return result;
        }

        // NEW: Hybrid approach - try boundary first, if fails use fixed length
        public static List<DetailLine> CreatePerpendicularLinesWithFallback(
            Document doc, View view, RoofBase roof, XYZ p1, XYZ p2, double fallbackLengthMeters = 3)
        {
            var result = new List<DetailLine>();

            try
            {
                // First attempt: try to find actual roof boundaries
                result = CreatePerpendicularLines(doc, view, roof, p1, p2);

                // Check if we got valid intersections (not just the fallback points)
                XYZ mid = (p1 + p2) / 2;
                XYZ dir = (p2 - p1).Normalize();
                XYZ perp = new XYZ(-dir.Y, dir.X, 0);

                bool hasValidLeftBoundary = false;
                bool hasValidRightBoundary = false;

                foreach (var line in result)
                {
                    Line ln = line.GeometryCurve as Line;
                    if (ln == null) continue;

                    XYZ endPoint = ln.GetEndPoint(1);
                    XYZ direction = (endPoint - mid).Normalize();

                    // Check if this line points to a real boundary (not just fixed length)
                    double dotProduct = direction.DotProduct(perp);
                    double length = mid.DistanceTo(endPoint);
                    double fixedLength = UnitUtils.ConvertToInternalUnits(fallbackLengthMeters, UnitTypeId.Meters);

                    if (Math.Abs(Math.Abs(dotProduct) - 1) < 0.01 && length > fixedLength * 1.1) // More than 10% longer than fixed
                    {
                        if (dotProduct > 0)
                            hasValidRightBoundary = true;
                        else
                            hasValidLeftBoundary = true;
                    }
                }

                // If we're missing boundaries, create fixed length lines for missing sides
                if (!hasValidLeftBoundary || !hasValidRightBoundary)
                {
                    // Clear and recreate with proper combination
                    result.Clear();

                    double fixedLength = UnitUtils.ConvertToInternalUnits(fallbackLengthMeters, UnitTypeId.Meters);

                    // Always create left side (fixed or try boundary again)
                    if (hasValidLeftBoundary)
                    {
                        // Left boundary already existed in previous result, but we need to re-fetch it
                        var boundaryResult = CreatePerpendicularLines(doc, view, roof, p1, p2);
                        var leftBoundary = boundaryResult.FirstOrDefault(l =>
                        {
                            Line ln = l.GeometryCurve as Line;
                            if (ln == null) return false;
                            XYZ end = ln.GetEndPoint(1);
                            return (end - mid).DotProduct(perp) < 0;
                        });

                        if (leftBoundary != null)
                            result.Add(leftBoundary);
                    }
                    else
                    {
                        result.Add(CreateDetailLine(doc, view, mid, mid - perp * fixedLength));
                    }

                    // Right side
                    if (hasValidRightBoundary)
                    {
                        var boundaryResult = CreatePerpendicularLines(doc, view, roof, p1, p2);
                        var rightBoundary = boundaryResult.FirstOrDefault(l =>
                        {
                            Line ln = l.GeometryCurve as Line;
                            if (ln == null) return false;
                            XYZ end = ln.GetEndPoint(1);
                            return (end - mid).DotProduct(perp) > 0;
                        });

                        if (rightBoundary != null)
                            result.Add(rightBoundary);
                    }
                    else
                    {
                        result.Add(CreateDetailLine(doc, view, mid, mid + perp * fixedLength));
                    }
                }
            }
            catch (Exception ex)
            {
                // If anything fails, fall back to fixed length
                Logger.LogException(ex, "CreatePerpendicularLinesWithFallback - falling back to fixed length");
                result = CreateFixedLengthPerpendicularLines(doc, view, p1, p2, fallbackLengthMeters);
            }

            return result;
        }

        // Update AddShapePoints to handle fallback lines correctly
        public static int AddShapePoints(
            Document doc, RoofBase roof, List<DetailLine> lines, double meters)
        {
            if (!lines.Any()) return 0;

            var editor = roof.GetSlabShapeEditor();
            editor.Enable();

            int count = 0;
            double minDistance = UnitUtils.ConvertToInternalUnits(0.1, UnitTypeId.Meters); // 10cm minimum

            foreach (var dl in lines)
            {
                Line ln = dl.GeometryCurve as Line;
                if (ln == null) continue;

                XYZ intersectionPoint = ln.GetEndPoint(1); // The far end point

                try
                {
                    // Check if point is within roof boundaries (approximate)
                    // If it's too far outside, skip or adjust
                    editor.AddPoint(intersectionPoint);
                    count++;
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Point might be outside roof - try to project onto roof
                    try
                    {
                        // Project point onto roof plane
                        Plane roofPlane = GetRoofPlane(roof);
                        if (roofPlane != null)
                        {
                            XYZ projected = ProjectOnto(roofPlane, intersectionPoint);
                            if (projected.DistanceTo(intersectionPoint) < minDistance * 10)
                            {
                                editor.AddPoint(projected);
                                count++;
                            }
                        }
                    }
                    catch
                    {
                        // Skip this point if it really can't be added
                        continue;
                    }
                }
            }

            return count;
        }

        // Helper to get roof plane
        private static Plane GetRoofPlane(RoofBase roof)
        {
            var geo = roof.get_Geometry(new Options());
            if (geo == null) return null;

            foreach (var obj in geo)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        PlanarFace planarFace = face as PlanarFace;
                        if (planarFace != null && Math.Abs(planarFace.FaceNormal.Z) > 0.9)
                        {
                            // Found a horizontal face
                            return Plane.CreateByNormalAndOrigin(
                                planarFace.FaceNormal,
                                planarFace.Origin);
                        }
                    }
                }
                else if (obj is GeometryInstance inst)
                {
                    var instGeo = inst.GetInstanceGeometry();
                    foreach (var instSolid in instGeo.OfType<Solid>())
                    {
                        foreach (Face face in instSolid.Faces)
                        {
                            PlanarFace planarFace = face as PlanarFace;
                            if (planarFace != null && Math.Abs(planarFace.FaceNormal.Z) > 0.9)
                            {
                                return Plane.CreateByNormalAndOrigin(
                                    planarFace.FaceNormal,
                                    planarFace.Origin);
                            }
                        }
                    }
                }
            }
            return null;
        }

        // Extension method for plane projection
        private static XYZ ProjectOnto(Plane plane, XYZ point)
        {
            XYZ vectorToPoint = point - plane.Origin;
            double distance = plane.Normal.DotProduct(vectorToPoint);
            return point - plane.Normal * distance;
        }

        private static IEnumerable<Curve> GetRoofEdges(RoofBase roof)
        {
            var options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            };

            var geo = roof.get_Geometry(options);
            if (geo == null) yield break;

            foreach (var obj in geo)
            {
                if (obj is Solid solid)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        var curve = edge.AsCurve();
                        if (curve != null && IsPlanarEdge(curve))
                            yield return curve;
                    }
                }
                else if (obj is GeometryInstance inst)
                {
                    var instGeo = inst.GetInstanceGeometry();
                    foreach (var geomObj in instGeo)
                    {
                        if (geomObj is Solid instSolid)
                        {
                            foreach (Edge edge in instSolid.Edges)
                            {
                                var curve = edge.AsCurve();
                                if (curve != null && IsPlanarEdge(curve))
                                    yield return curve;
                            }
                        }
                    }
                }
            }
        }

        // Helper to filter horizontal edges
        private static bool IsPlanarEdge(Curve curve)
        {
            // Only consider edges that are roughly horizontal
            if (curve is Line line)
            {
                XYZ dir = line.Direction;
                return Math.Abs(dir.Z) < 0.01; // Near horizontal
            }
            return false;
        }
    }
}