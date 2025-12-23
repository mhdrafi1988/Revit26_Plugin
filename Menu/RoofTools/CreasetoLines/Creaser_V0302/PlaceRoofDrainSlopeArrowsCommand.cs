// ============================================================
// File: PlaceRoofDrainSlopeArrowsCommand.cs
// Version: Creaser_V03_05 (Complete Coverage)
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Creaser_V03_02.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserCommand : IExternalCommand
    {
        private const double Z_TOL = 0.001;  // 1mm tolerance
        private const double POINT_TOL = 0.001;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            View view = doc?.ActiveView;

            if (doc == null || view == null || view.ViewType != ViewType.FloorPlan)
            {
                message = "Command works only in plan views.";
                return Result.Failed;
            }

            // Get ALL roofs
            List<RoofBase> roofs = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofBase))
                .Cast<RoofBase>()
                .ToList();

            if (roofs.Count == 0)
            {
                TaskDialog.Show("Error", "No roof found in the project.");
                return Result.Failed;
            }

            // Get detail component family
            FamilySymbol symbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Family.FamilyPlacementType == FamilyPlacementType.CurveBasedDetail);

            if (symbol == null)
            {
                TaskDialog.Show("Error", "No curve-based detail component family found.");
                return Result.Cancelled;
            }

            // Build graph
            Dictionary<XYZKey, List<XYZKey>> graph = new Dictionary<XYZKey, List<XYZKey>>();
            HashSet<XYZKey> boundaryNodes = new HashSet<XYZKey>();
            HashSet<XYZKey> allNodes = new HashSet<XYZKey>();

            // Helper: Snap point to tolerance
            XYZKey SnapPoint(XYZ point)
            {
                return new XYZKey(
                    Math.Round(point.X / POINT_TOL) * POINT_TOL,
                    Math.Round(point.Y / POINT_TOL) * POINT_TOL,
                    Math.Round(point.Z / Z_TOL) * Z_TOL);
            }

            int edgeCount = 0;

            foreach (RoofBase roof in roofs)
            {
                Options opt = new Options
                {
                    DetailLevel = ViewDetailLevel.Fine,
                    ComputeReferences = true
                };

                GeometryElement geom = roof.get_Geometry(opt);
                if (geom == null) continue;

                foreach (GeometryObject obj in geom)
                {
                    if (obj is Solid solid)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face is PlanarFace planarFace && Math.Abs(planarFace.FaceNormal.Z) > 0.9)
                            {
                                foreach (EdgeArray loop in planarFace.EdgeLoops)
                                {
                                    List<XYZ> points = new List<XYZ>();
                                    foreach (Edge edge in loop)
                                    {
                                        Curve curve = edge.AsCurve();
                                        if (curve != null)
                                        {
                                            points.Add(curve.GetEndPoint(0));
                                        }
                                    }

                                    // Add boundary nodes
                                    foreach (XYZ point in points)
                                    {
                                        XYZKey snapped = SnapPoint(point);
                                        boundaryNodes.Add(snapped);
                                        allNodes.Add(snapped);
                                    }

                                    // Create downhill edges
                                    for (int i = 0; i < points.Count; i++)
                                    {
                                        XYZKey p1 = SnapPoint(points[i]);
                                        XYZKey p2 = SnapPoint(points[(i + 1) % points.Count]);

                                        if (!graph.ContainsKey(p1)) graph[p1] = new List<XYZKey>();
                                        if (!graph.ContainsKey(p2)) graph[p2] = new List<XYZKey>();

                                        // Add edge based on elevation
                                        if (p1.Z > p2.Z + Z_TOL)
                                        {
                                            if (!graph[p1].Contains(p2))
                                            {
                                                graph[p1].Add(p2);
                                                edgeCount++;
                                            }
                                        }
                                        else if (p2.Z > p1.Z + Z_TOL)
                                        {
                                            if (!graph[p2].Contains(p1))
                                            {
                                                graph[p2].Add(p1);
                                                edgeCount++;
                                            }
                                        }
                                        else // Equal elevation
                                        {
                                            if (!graph[p1].Contains(p2))
                                            {
                                                graph[p1].Add(p2);
                                                edgeCount++;
                                            }
                                            if (!graph[p2].Contains(p1))
                                            {
                                                graph[p2].Add(p1);
                                                edgeCount++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (graph.Count == 0)
            {
                TaskDialog.Show("Error", "No drainage graph created.");
                return Result.Failed;
            }

            // Find drain nodes (lowest points)
            double minZ = graph.Keys.Min(p => p.Z);
            HashSet<XYZKey> drainNodes = new HashSet<XYZKey>(
                graph.Keys.Where(p => Math.Abs(p.Z - minZ) < Z_TOL * 2));

            // Find ridge nodes (highest boundary points)
            double maxZ = boundaryNodes.Max(p => p.Z);
            HashSet<XYZKey> ridgeNodes = new HashSet<XYZKey>(
                boundaryNodes.Where(p => Math.Abs(p.Z - maxZ) < Z_TOL * 2));

            // Identify corner nodes (boundary nodes with exactly 2 connections in graph)
            HashSet<XYZKey> cornerNodes = new HashSet<XYZKey>();
            foreach (XYZKey node in boundaryNodes)
            {
                if (graph.ContainsKey(node))
                {
                    int outgoing = graph[node].Count;
                    int incoming = 0;

                    // Count incoming edges
                    foreach (var kv in graph)
                    {
                        if (kv.Value.Contains(node))
                            incoming++;
                    }

                    int totalConnections = outgoing + incoming;
                    if (totalConnections == 2)
                        cornerNodes.Add(node);
                }
            }

            // Other boundary nodes (not corners, not ridges)
            HashSet<XYZKey> otherBoundaryNodes = new HashSet<XYZKey>(boundaryNodes);
            otherBoundaryNodes.ExceptWith(cornerNodes);
            otherBoundaryNodes.ExceptWith(ridgeNodes);

            // Group ridge nodes into lines for proper left/right determination
            List<List<XYZKey>> ridgeLines = GroupRidgeNodesIntoLines(ridgeNodes, graph);

            // Start transaction
            using (Transaction tx = new Transaction(doc, "Place Roof Drain Arrows"))
            {
                tx.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                int placedCount = 0;
                int pathCount = 0;

                // Local function to place a path
                void PlacePath(List<XYZKey> path, string pathType = "")
                {
                    if (path == null || path.Count < 2) return;

                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        XYZ p1 = path[i].ToXYZ();
                        XYZ p2 = path[i + 1].ToXYZ();

                        if (p1.DistanceTo(p2) < doc.Application.ShortCurveTolerance * 2)
                            continue;

                        // Project to view plane
                        if (view.SketchPlane != null)
                        {
                            try
                            {
                                Plane plane = view.SketchPlane.GetPlane();
                                XYZ v1 = p1 - plane.Origin;
                                XYZ v2 = p2 - plane.Origin;
                                p1 = p1 - plane.Normal * v1.DotProduct(plane.Normal);
                                p2 = p2 - plane.Normal * v2.DotProduct(plane.Normal);
                            }
                            catch { }
                        }

                        try
                        {
                            Line line = Line.CreateBound(p1, p2);
                            if (line.Length > doc.Application.ShortCurveTolerance)
                            {
                                FamilyInstance arrow = doc.Create.NewFamilyInstance(line, symbol, view);
                                placedCount++;
                            }
                        }
                        catch { }
                    }
                    pathCount++;
                }

                // ==================== 1. CORNER POINTS ====================
                TaskDialog.Show("Processing", $"Processing {cornerNodes.Count} corner points...");
                foreach (XYZKey corner in cornerNodes)
                {
                    if (!graph.ContainsKey(corner) || graph[corner].Count == 0)
                        continue;

                    var path = DrainPathSolver.FindShortestPathBFS(corner, drainNodes, graph);
                    if (path.Count > 1)
                    {
                        PlacePath(path, "Corner");
                    }
                }

                // ==================== 2. RIDGE POINTS (Both Sides) ====================
                TaskDialog.Show("Processing", $"Processing {ridgeNodes.Count} ridge points in {ridgeLines.Count} lines...");

                foreach (var ridgeLine in ridgeLines)
                {
                    if (ridgeLine.Count == 0) continue;

                    // For each ridge point in the line
                    foreach (XYZKey ridgePoint in ridgeLine)
                    {
                        if (!graph.ContainsKey(ridgePoint) || graph[ridgePoint].Count == 0)
                            continue;

                        // Get ridge line direction
                        XYZ ridgeDir = GetRidgeDirection(ridgePoint, ridgeLine, graph);

                        if (ridgeDir != null)
                        {
                            // Split drains into left and right groups
                            var (leftDrains, rightDrains) = SplitDrainsBySide(ridgePoint, ridgeDir, drainNodes);

                            // LEFT side path
                            if (leftDrains.Any())
                            {
                                var leftPath = DrainPathSolver.FindShortestPathBFS(ridgePoint, leftDrains, graph);
                                if (leftPath.Count > 1)
                                {
                                    PlacePath(leftPath, "Ridge-Left");
                                }
                            }

                            // RIGHT side path
                            if (rightDrains.Any())
                            {
                                var rightPath = DrainPathSolver.FindShortestPathBFS(ridgePoint, rightDrains, graph);
                                if (rightPath.Count > 1)
                                {
                                    PlacePath(rightPath, "Ridge-Right");
                                }
                            }
                        }
                        else
                        {
                            // Fallback: single path if can't determine direction
                            var path = DrainPathSolver.FindShortestPathBFS(ridgePoint, drainNodes, graph);
                            if (path.Count > 1)
                            {
                                PlacePath(path, "Ridge-Fallback");
                            }
                        }
                    }
                }

                // ==================== 3. OTHER BOUNDARY POINTS ====================
                TaskDialog.Show("Processing", $"Processing {otherBoundaryNodes.Count} other boundary points...");
                foreach (XYZKey otherPoint in otherBoundaryNodes)
                {
                    if (!graph.ContainsKey(otherPoint) || graph[otherPoint].Count == 0)
                        continue;

                    var path = DrainPathSolver.FindShortestPathBFS(otherPoint, drainNodes, graph);
                    if (path.Count > 1)
                    {
                        PlacePath(path, "Other");
                    }
                }

                // ==================== 4. FALLBACK: Direct connections ====================
                // If still no arrows, create direct connections
                if (placedCount == 0)
                {
                    TaskDialog.Show("Fallback", "Using direct connections...");

                    foreach (XYZKey startNode in boundaryNodes)
                    {
                        if (!graph.ContainsKey(startNode)) continue;

                        // Find nearest drain
                        XYZKey nearestDrain = drainNodes.First();
                        double minDist = double.MaxValue;

                        foreach (XYZKey drain in drainNodes)
                        {
                            double dist = startNode.DistanceTo2D(drain);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                nearestDrain = drain;
                            }
                        }

                        var directPath = new List<XYZKey> { startNode, nearestDrain };
                        PlacePath(directPath, "Direct");
                    }
                }

                tx.Commit();

                // Results
                string resultMsg = $"RESULTS:\n" +
                                 $"Total arrows placed: {placedCount}\n" +
                                 $"Total paths created: {pathCount}\n" +
                                 $"Corner nodes: {cornerNodes.Count}\n" +
                                 $"Ridge nodes: {ridgeNodes.Count}\n" +
                                 $"Other boundary nodes: {otherBoundaryNodes.Count}\n" +
                                 $"Drain nodes: {drainNodes.Count}";

                TaskDialog.Show("Complete", resultMsg);
            }

            return Result.Succeeded;
        }

        // ==================== HELPER METHODS ====================

        // Group ridge nodes into lines
        private List<List<XYZKey>> GroupRidgeNodesIntoLines(HashSet<XYZKey> ridgeNodes, Dictionary<XYZKey, List<XYZKey>> graph)
        {
            List<List<XYZKey>> lines = new List<List<XYZKey>>();
            HashSet<XYZKey> processed = new HashSet<XYZKey>();

            foreach (XYZKey node in ridgeNodes)
            {
                if (processed.Contains(node)) continue;

                List<XYZKey> line = new List<XYZKey> { node };
                processed.Add(node);

                // Find connected ridge nodes
                Queue<XYZKey> queue = new Queue<XYZKey>();
                queue.Enqueue(node);

                while (queue.Count > 0)
                {
                    XYZKey current = queue.Dequeue();

                    if (graph.ContainsKey(current))
                    {
                        foreach (XYZKey neighbor in graph[current])
                        {
                            if (ridgeNodes.Contains(neighbor) && !processed.Contains(neighbor))
                            {
                                line.Add(neighbor);
                                processed.Add(neighbor);
                                queue.Enqueue(neighbor);
                            }
                        }
                    }
                }

                if (line.Count > 1)
                {
                    // Sort line points
                    line = SortPointsAlongLine(line);
                    lines.Add(line);
                }
            }

            return lines;
        }

        // Get direction of ridge line at a point
        private XYZ GetRidgeDirection(XYZKey ridgePoint, List<XYZKey> ridgeLine, Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (ridgeLine.Count < 2) return null;

            int index = ridgeLine.IndexOf(ridgePoint);
            if (index < 0) return null;

            XYZ pointXYZ = ridgePoint.ToXYZ();

            // Try to get direction from neighboring ridge points
            if (index > 0 && index < ridgeLine.Count - 1)
            {
                XYZ prev = ridgeLine[index - 1].ToXYZ();
                XYZ next = ridgeLine[index + 1].ToXYZ();
                return (next - prev).Normalize();
            }
            else if (index == 0 && ridgeLine.Count > 1)
            {
                XYZ next = ridgeLine[1].ToXYZ();
                return (next - pointXYZ).Normalize();
            }
            else if (index == ridgeLine.Count - 1 && ridgeLine.Count > 1)
            {
                XYZ prev = ridgeLine[ridgeLine.Count - 2].ToXYZ();
                return (pointXYZ - prev).Normalize();
            }

            return null;
        }

        // Split drains into left and right groups based on ridge direction
        private (HashSet<XYZKey> left, HashSet<XYZKey> right) SplitDrainsBySide(
            XYZKey ridgePoint, XYZ ridgeDir, HashSet<XYZKey> drainNodes)
        {
            HashSet<XYZKey> leftDrains = new HashSet<XYZKey>();
            HashSet<XYZKey> rightDrains = new HashSet<XYZKey>();

            if (ridgeDir == null || drainNodes.Count == 0)
                return (leftDrains, rightDrains);

            XYZ ridgeXYZ = ridgePoint.ToXYZ();
            XYZ normal = new XYZ(-ridgeDir.Y, ridgeDir.X, 0).Normalize();

            foreach (XYZKey drain in drainNodes)
            {
                XYZ drainXYZ = drain.ToXYZ();
                XYZ toDrain = (drainXYZ - ridgeXYZ).Normalize();

                double cross = normal.X * toDrain.Y - normal.Y * toDrain.X;

                if (cross > 0)
                    leftDrains.Add(drain);
                else if (cross < 0)
                    rightDrains.Add(drain);
                else
                {
                    // On the line - split evenly
                    if (leftDrains.Count <= rightDrains.Count)
                        leftDrains.Add(drain);
                    else
                        rightDrains.Add(drain);
                }
            }

            return (leftDrains, rightDrains);
        }

        // Sort points along a line
        private List<XYZKey> SortPointsAlongLine(List<XYZKey> points)
        {
            if (points.Count <= 2) return points;

            // Find endpoints (points with minimum and maximum projection)
            XYZKey start = points[0];
            XYZKey end = points[0];
            double minProj = double.MaxValue;
            double maxProj = double.MinValue;

            // Simple sort by X then Y
            return points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        }
    }

    // ==================== XYZKey STRUCT ====================
    internal readonly struct XYZKey
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public XYZKey(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public XYZKey(XYZ point) : this(point.X, point.Y, point.Z) { }

        public XYZ ToXYZ() => new XYZ(X, Y, Z);

        public double DistanceTo(XYZKey other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public double DistanceTo2D(XYZKey other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is XYZKey)) return false;
            XYZKey other = (XYZKey)obj;
            return Math.Abs(X - other.X) < 1e-6 &&
                   Math.Abs(Y - other.Y) < 1e-6 &&
                   Math.Abs(Z - other.Z) < 1e-6;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Math.Round(X, 6),
                Math.Round(Y, 6),
                Math.Round(Z, 6));
        }

        public override string ToString()
        {
            return $"({X:F3}, {Y:F3}, {Z:F3})";
        }
    }
}