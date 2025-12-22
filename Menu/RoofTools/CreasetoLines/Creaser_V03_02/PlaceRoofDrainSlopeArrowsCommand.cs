// ============================================================
// File: PlaceRoofDrainSlopeArrowsCommand.cs
// Version: Creaser_V03_04 (Debugged)
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

            // Get ALL roofs (not just first one)
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
                TaskDialog.Show("Error", "No curve-based detail component family found.\nPlease load a line-based detail component family.");
                return Result.Cancelled;
            }

            // Start with diagnostic messages
            string debugInfo = $"Found {roofs.Count} roof(s) in project.\n";

            // Build graph from ALL roofs
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

            int roofCount = 0;
            int faceCount = 0;
            int edgeCount = 0;

            foreach (RoofBase roof in roofs)
            {
                roofCount++;

                // Get geometry
                Options opt = new Options
                {
                    DetailLevel = ViewDetailLevel.Fine,
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true
                };

                GeometryElement geom = roof.get_Geometry(opt);

                if (geom == null) continue;

                // Build edges from geometry
                foreach (GeometryObject obj in geom)
                {
                    if (obj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face is PlanarFace planarFace)
                            {
                                // Accept faces that are mostly horizontal (normal Z close to 1 or -1)
                                if (Math.Abs(Math.Abs(planarFace.FaceNormal.Z) - 1.0) < 0.1)
                                {
                                    faceCount++;

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

                                        if (points.Count > 0)
                                        {
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

                                                // Ensure nodes exist in graph
                                                if (!graph.ContainsKey(p1)) graph[p1] = new List<XYZKey>();
                                                if (!graph.ContainsKey(p2)) graph[p2] = new List<XYZKey>();

                                                // Add edge based on elevation (downhill only)
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
                                                // If elevations are equal, create bidirectional edge for flat surfaces
                                                else if (Math.Abs(p1.Z - p2.Z) < Z_TOL)
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
                }
            }

            debugInfo += $"Processed {roofCount} roofs, {faceCount} faces, created {graph.Count} nodes and {edgeCount} edges.\n";

            if (graph.Count == 0)
            {
                TaskDialog.Show("Error", $"No drainage graph created.\n{debugInfo}\nCheck if roof has slope.");
                return Result.Failed;
            }

            // Find drain nodes (lowest points)
            double minZ = graph.Keys.Min(p => p.Z);
            HashSet<XYZKey> drainNodes = new HashSet<XYZKey>(
                graph.Keys.Where(p => Math.Abs(p.Z - minZ) < Z_TOL * 2));

            // Find ridge nodes (highest boundary points)
            double maxZ = boundaryNodes.Count > 0 ? boundaryNodes.Max(p => p.Z) : minZ;
            HashSet<XYZKey> ridgeNodes = new HashSet<XYZKey>(
                boundaryNodes.Where(p => Math.Abs(p.Z - maxZ) < Z_TOL * 2));

            debugInfo += $"Drain nodes: {drainNodes.Count} (min Z={minZ:F3})\n";
            debugInfo += $"Ridge nodes: {ridgeNodes.Count} (max Z={maxZ:F3})\n";
            debugInfo += $"Boundary nodes: {boundaryNodes.Count}\n";

            // Show debug info
            TaskDialog.Show("Debug Info", debugInfo);

            if (drainNodes.Count == 0)
            {
                TaskDialog.Show("Error", "No drain points (low points) found in roof.");
                return Result.Failed;
            }

            // Start transaction
            using (Transaction tx = new Transaction(doc, "Place Roof Drain Arrows"))
            {
                try
                {
                    tx.Start();

                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        doc.Regenerate();
                    }

                    int placedCount = 0;

                    // Local function to place a path
                    void PlacePath(List<XYZKey> path)
                    {
                        if (path == null || path.Count < 2) return;

                        for (int i = 0; i < path.Count - 1; i++)
                        {
                            XYZ p1 = path[i].ToXYZ();
                            XYZ p2 = path[i + 1].ToXYZ();

                            // Skip if points are too close
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
                                catch
                                {
                                    // If projection fails, use original points
                                }
                            }

                            // Create line
                            try
                            {
                                Line line = Line.CreateBound(p1, p2);
                                if (line.Length > doc.Application.ShortCurveTolerance)
                                {
                                    FamilyInstance arrow = doc.Create.NewFamilyInstance(line, symbol, view);
                                    placedCount++;

                                    // Optional: Set arrow parameters
                                    // arrow.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set("Drainage Arrow");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Skip this arrow if it fails
                            }
                        }
                    }

                    // TEST: Place direct edges from boundary nodes to drains for debugging
                    bool testMode = true;
                    if (testMode)
                    {
                        debugInfo += "\n=== TEST MODE ===\n";

                        // Try placing simple arrows from each boundary node to nearest drain
                        foreach (XYZKey startNode in boundaryNodes)
                        {
                            if (!graph.ContainsKey(startNode) || graph[startNode].Count == 0)
                                continue;

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

                            // Create direct path
                            var testPath = new List<XYZKey> { startNode, nearestDrain };
                            PlacePath(testPath);
                            debugInfo += $"Test path: {startNode} -> {nearestDrain}\n";
                        }
                    }
                    else
                    {
                        // Normal mode: Use pathfinding
                        foreach (XYZKey startNode in boundaryNodes)
                        {
                            if (!graph.ContainsKey(startNode) || graph[startNode].Count == 0)
                                continue;

                            // For ridge nodes
                            if (ridgeNodes.Contains(startNode) && drainNodes.Count >= 2)
                            {
                                // Split drains into two groups
                                var drainsList = drainNodes.ToList();
                                int half = drainsList.Count / 2;

                                var group1 = new HashSet<XYZKey>(drainsList.Take(half));
                                var group2 = new HashSet<XYZKey>(drainsList.Skip(half));

                                // Place path to group 1
                                if (group1.Any())
                                {
                                    var path1 = DrainPathSolver.FindShortestPathBFS(startNode, group1, graph);
                                    if (path1.Count > 1)
                                    {
                                        PlacePath(path1);
                                    }
                                }

                                // Place path to group 2
                                if (group2.Any())
                                {
                                    var path2 = DrainPathSolver.FindShortestPathBFS(startNode, group2, graph);
                                    if (path2.Count > 1)
                                    {
                                        PlacePath(path2);
                                    }
                                }
                            }
                            else
                            {
                                // Single path for non-ridge points
                                var path = DrainPathSolver.FindShortestPathBFS(startNode, drainNodes, graph);
                                if (path.Count > 1)
                                {
                                    PlacePath(path);
                                }
                            }
                        }
                    }

                    tx.Commit();

                    if (placedCount > 0)
                    {
                        TaskDialog.Show("Success", $"Placed {placedCount} drainage arrows.\n\n{debugInfo}");
                    }
                    else
                    {
                        TaskDialog.Show("Warning",
                            $"No arrows were placed.\n\nDebug Info:\n{debugInfo}\n\n" +
                            "Possible issues:\n" +
                            "1. Roof might be flat (no slope)\n" +
                            "2. No valid paths from boundaries to drains\n" +
                            "3. Detail component family issue\n" +
                            "4. View might not support detail components");
                    }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Transaction Error", $"Failed to place arrows: {ex.Message}\n{ex.StackTrace}");
                    return Result.Failed;
                }
            }

            return Result.Succeeded;
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