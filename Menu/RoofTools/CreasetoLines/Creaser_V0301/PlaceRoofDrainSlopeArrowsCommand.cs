// ============================================================
// File: PlaceRoofDrainSlopeArrowsCommand.cs
// Version: Creaser_V03_01
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Creaser_V03_01.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserCommand : IExternalCommand
    {
        private const double Z_TOL = 1e-6;
        private const double POINT_TOL = 1e-4;
        private const double COLINEAR_TOL = 1e-3;

        private readonly Dictionary<XYZKey, XYZKey> _snapCache =
            new Dictionary<XYZKey, XYZKey>();

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            View view = doc?.ActiveView;

            if (doc == null ||
                view == null ||
                view.ViewType != ViewType.FloorPlan ||
                view.SketchPlane == null)
            {
                message = "Command works only in plan views.";
                return Result.Failed;
            }

            RoofBase roof = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofBase))
                .Cast<RoofBase>()
                .FirstOrDefault();

            if (roof == null)
            {
                message = "No roof found.";
                return Result.Failed;
            }

            FamilySymbol symbol = SelectFamily(doc);
            if (symbol == null)
                return Result.Cancelled;

            Options opt = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = false
            };

            GeometryElement geom = roof.get_Geometry(opt);

            Dictionary<XYZKey, List<XYZKey>> graph =
                new Dictionary<XYZKey, List<XYZKey>>();

            HashSet<XYZKey> boundaryNodes = new HashSet<XYZKey>();
            Dictionary<XYZKey, int> boundaryDegree =
                new Dictionary<XYZKey, int>();

            // ---------------- Helpers ----------------

            XYZKey Snap(XYZKey p)
            {
                foreach (XYZKey e in _snapCache.Keys)
                {
                    if (e.DistanceTo(p) < POINT_TOL)
                        return e;
                }

                _snapCache[p] = p;
                return p;
            }

            void EnsureNode(XYZKey p)
            {
                if (!graph.ContainsKey(p))
                    graph[p] = new List<XYZKey>();
            }

            void AddDownhillEdge(XYZKey a, XYZKey b)
            {
                double dz = a.Z - b.Z;

                if (dz > Z_TOL)
                {
                    EnsureNode(a);
                    EnsureNode(b);
                    graph[a].Add(b);
                }
                else if (-dz > Z_TOL)
                {
                    EnsureNode(a);
                    EnsureNode(b);
                    graph[b].Add(a);
                }
            }

            // ---------------- Build graph ----------------

            foreach (GeometryObject obj in geom)
            {
                Solid solid = obj as Solid;
                if (solid == null) continue;

                foreach (Face face in solid.Faces)
                {
                    PlanarFace pf = face as PlanarFace;
                    if (pf == null) continue;
                    if (pf.FaceNormal.Z < 0.99) continue;

                    bool outer = true;

                    foreach (EdgeArray loop in pf.EdgeLoops)
                    {
                        List<XYZKey> pts = loop
                            .Cast<Edge>()
                            .Select(e => Snap(
                                new XYZKey(e.AsCurve().GetEndPoint(0))))
                            .ToList();

                        if (outer)
                        {
                            foreach (XYZKey p in pts)
                            {
                                boundaryNodes.Add(p);
                                boundaryDegree[p] =
                                    boundaryDegree.ContainsKey(p)
                                        ? boundaryDegree[p] + 1
                                        : 1;
                            }
                            outer = false;
                        }

                        for (int i = 0; i < pts.Count; i++)
                        {
                            AddDownhillEdge(
                                pts[i],
                                pts[(i + 1) % pts.Count]);
                        }
                    }
                }
            }

            if (!graph.Any())
            {
                message = "Failed to build drainage graph.";
                return Result.Failed;
            }

            // ---------------- Classify nodes ----------------

            HashSet<XYZKey> cornerNodes = new HashSet<XYZKey>();
            foreach (KeyValuePair<XYZKey, int> kv in boundaryDegree)
            {
                if (kv.Value == 2)
                    cornerNodes.Add(kv.Key);
            }

            double minZ = graph.Keys.Min(p => p.Z);
            HashSet<XYZKey> drainNodes = new HashSet<XYZKey>(
                graph.Keys.Where(p => Math.Abs(p.Z - minZ) < Z_TOL));

            // Identify ridge points (highest boundary points)
            double maxZ = graph.Keys.Max(p => p.Z);
            HashSet<XYZKey> ridgeNodes = new HashSet<XYZKey>(
                boundaryNodes.Where(p => Math.Abs(p.Z - maxZ) < Z_TOL));

            // Remove corner nodes from ridge nodes if they happen to be highest
            ridgeNodes.ExceptWith(cornerNodes);

            Plane plane = view.SketchPlane.GetPlane();
            double minLen = doc.Application.ShortCurveTolerance;
            HashSet<string> placed = new HashSet<string>();

            using (Transaction tx =
                new Transaction(doc, "Place Roof Drain Slope Arrows"))
            {
                tx.Start();

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    doc.Regenerate();
                }

                // ---------- 1) Corners → shortest path ----------
                foreach (XYZKey corner in cornerNodes)
                {
                    if (!graph.ContainsKey(corner)) continue;

                    List<XYZKey> path =
                        DrainPathSolver.FindShortestPath(
                            corner, drainNodes, graph);

                    for (int i = 0; i < path.Count - 1; i++)
                        Place(path[i], path[i + 1]);
                }

                // ---------- 2) Group ridge points into lines ----------
                List<List<XYZKey>> ridgeLines = GroupPointsIntoLines(ridgeNodes, COLINEAR_TOL);

                // ---------- 3) Ridge lines → left and right paths ----------
                foreach (var ridgeLine in ridgeLines)
                {
                    if (ridgeLine.Count == 0) continue;

                    // For each ridge point in the line, create paths to left and right
                    foreach (XYZKey ridgePoint in ridgeLine)
                    {
                        if (!graph.ContainsKey(ridgePoint) || graph[ridgePoint].Count == 0)
                            continue;

                        // Find drain points on left and right of the ridge line
                        var (leftDrains, rightDrains) = FindDrainsBySide(
                            ridgePoint, ridgeLine, drainNodes, graph);

                        // Create path to left side drain
                        if (leftDrains.Any())
                        {
                            List<XYZKey> leftPath = DrainPathSolver.FindShortestPath(
                                ridgePoint, leftDrains, graph);

                            if (leftPath.Count > 1)
                            {
                                for (int i = 0; i < leftPath.Count - 1; i++)
                                    Place(leftPath[i], leftPath[i + 1]);
                            }
                        }

                        // Create path to right side drain
                        if (rightDrains.Any())
                        {
                            List<XYZKey> rightPath = DrainPathSolver.FindShortestPath(
                                ridgePoint, rightDrains, graph);

                            if (rightPath.Count > 1)
                            {
                                for (int i = 0; i < rightPath.Count - 1; i++)
                                    Place(rightPath[i], rightPath[i + 1]);
                            }
                        }
                    }
                }

                // ---------- 4) Other boundary points → single shortest path ----------
                HashSet<XYZKey> otherBoundaryNodes = new HashSet<XYZKey>(boundaryNodes);
                otherBoundaryNodes.ExceptWith(cornerNodes);
                otherBoundaryNodes.ExceptWith(ridgeNodes);

                foreach (XYZKey boundaryPoint in otherBoundaryNodes)
                {
                    if (!graph.ContainsKey(boundaryPoint)) continue;
                    if (graph[boundaryPoint].Count == 0) continue;

                    List<XYZKey> path = DrainPathSolver.FindShortestPath(
                        boundaryPoint, drainNodes, graph);

                    if (path.Count > 1)
                    {
                        for (int i = 0; i < path.Count - 1; i++)
                            Place(path[i], path[i + 1]);
                    }
                }

                tx.Commit();
            }

            return Result.Succeeded;

            // ---------- Placement helper ----------
            void Place(XYZKey a, XYZKey b)
            {
                XYZ p1 = Project(a.ToXYZ(), plane);
                XYZ p2 = Project(b.ToXYZ(), plane);

                if (p1.DistanceTo(p2) < minLen) return;

                string key =
                    $"{p1.X:F6}_{p1.Y:F6}|{p2.X:F6}_{p2.Y:F6}";

                if (!placed.Add(key)) return;

                doc.Create.NewFamilyInstance(
                    Line.CreateBound(p1, p2), symbol, view);
            }
        }

        // ==================== HELPER METHODS ====================

        private static XYZ Project(XYZ p, Plane plane)
        {
            XYZ v = p - plane.Origin;
            return p - v.DotProduct(plane.Normal) * plane.Normal;
        }

        private static FamilySymbol SelectFamily(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s =>
                    s.Family.FamilyPlacementType ==
                    FamilyPlacementType.CurveBasedDetail);
        }

        // Group points that form straight lines (within tolerance)
        private List<List<XYZKey>> GroupPointsIntoLines(HashSet<XYZKey> points, double tolerance)
        {
            List<List<XYZKey>> lines = new List<List<XYZKey>>();
            HashSet<XYZKey> processed = new HashSet<XYZKey>();

            // Convert to list for easier processing
            List<XYZKey> pointList = points.ToList();

            while (pointList.Count > 0)
            {
                XYZKey current = pointList[0];
                pointList.RemoveAt(0);

                // Start a new line with this point
                List<XYZKey> line = new List<XYZKey> { current };

                // Find all colinear points
                for (int i = pointList.Count - 1; i >= 0; i--)
                {
                    XYZKey testPoint = pointList[i];

                    // Check if this point is colinear with any two points in the line
                    bool isColinear = false;
                    for (int j = 0; j < line.Count - 1; j++)
                    {
                        if (IsPointOnLine(line[j], line[j + 1], testPoint, tolerance))
                        {
                            isColinear = true;
                            break;
                        }
                    }

                    // Also check with just the current point if line has only one point
                    if (line.Count == 1)
                    {
                        // Need at least one more point to determine direction
                        // We'll add it and check colinearity later
                        line.Add(testPoint);
                        pointList.RemoveAt(i);
                        continue;
                    }

                    if (isColinear)
                    {
                        line.Add(testPoint);
                        pointList.RemoveAt(i);
                    }
                }

                // Sort line points along their direction
                if (line.Count > 1)
                {
                    line = SortPointsAlongLine(line);
                }

                lines.Add(line);
            }

            return lines;
        }

        // Check if point C lies on line AB (within tolerance)
        private bool IsPointOnLine(XYZKey A, XYZKey B, XYZKey C, double tolerance)
        {
            // Vector AB
            double abx = B.X - A.X;
            double aby = B.Y - A.Y;

            // Vector AC
            double acx = C.X - A.X;
            double acy = C.Y - A.Y;

            // Cross product (should be near zero for colinear points)
            double cross = abx * acy - aby * acx;

            if (Math.Abs(cross) > tolerance) return false;

            // Check if C is between A and B (within bounds)
            double dot = acx * abx + acy * aby;
            double abLengthSq = abx * abx + aby * aby;

            // Allow some tolerance beyond endpoints
            return dot >= -tolerance && dot <= abLengthSq + tolerance;
        }

        // Sort points along a line
        private List<XYZKey> SortPointsAlongLine(List<XYZKey> points)
        {
            if (points.Count <= 2) return points;

            // Find the two points farthest apart to establish line direction
            XYZKey start = points[0];
            XYZKey end = points[0];
            double maxDist = 0;

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double dist = points[i].DistanceTo(points[j]);
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        start = points[i];
                        end = points[j];
                    }
                }
            }

            // Project all points onto the line and sort by projection
            XYZ lineDir = new XYZ(end.X - start.X, end.Y - start.Y, 0).Normalize();

            return points.OrderBy(p =>
            {
                XYZ vec = new XYZ(p.X - start.X, p.Y - start.Y, 0);
                return vec.DotProduct(lineDir);
            }).ToList();
        }

        // Find drains on left and right sides of a ridge line
        private (HashSet<XYZKey> leftDrains, HashSet<XYZKey> rightDrains)
            FindDrainsBySide(XYZKey ridgePoint, List<XYZKey> ridgeLine,
                            HashSet<XYZKey> allDrains,
                            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            HashSet<XYZKey> leftDrains = new HashSet<XYZKey>();
            HashSet<XYZKey> rightDrains = new HashSet<XYZKey>();

            if (ridgeLine.Count < 2)
            {
                // If only one point, split drains by angle
                return SplitDrainsByAngle(ridgePoint, allDrains);
            }

            // Find line direction (use neighboring points in the line)
            int index = ridgeLine.IndexOf(ridgePoint);
            XYZKey directionPoint;

            if (index < ridgeLine.Count - 1)
                directionPoint = ridgeLine[index + 1];  // Use next point
            else
                directionPoint = ridgeLine[index - 1];  // Use previous point

            // Calculate line direction vector
            XYZ ridgeXYZ = ridgePoint.ToXYZ();
            XYZ dirXYZ = directionPoint.ToXYZ();
            XYZ lineDirection = (dirXYZ - ridgeXYZ).Normalize();

            // Calculate perpendicular (normal) vector
            XYZ normal = new XYZ(-lineDirection.Y, lineDirection.X, 0).Normalize();

            foreach (XYZKey drain in allDrains)
            {
                if (drain.Equals(ridgePoint)) continue;

                XYZ drainXYZ = drain.ToXYZ();
                XYZ toDrain = (drainXYZ - ridgeXYZ).Normalize();

                // Cross product to determine side
                double cross = normal.X * toDrain.Y - normal.Y * toDrain.X;

                if (cross > POINT_TOL)
                    leftDrains.Add(drain);
                else if (cross < -POINT_TOL)
                    rightDrains.Add(drain);
                else
                {
                    // Exactly on the line - assign based on which side has fewer drains
                    if (leftDrains.Count <= rightDrains.Count)
                        leftDrains.Add(drain);
                    else
                        rightDrains.Add(drain);
                }
            }

            return (leftDrains, rightDrains);
        }

        // Split drains by angle when we don't have a clear ridge line
        private (HashSet<XYZKey> leftDrains, HashSet<XYZKey> rightDrains)
            SplitDrainsByAngle(XYZKey ridgePoint, HashSet<XYZKey> allDrains)
        {
            HashSet<XYZKey> leftDrains = new HashSet<XYZKey>();
            HashSet<XYZKey> rightDrains = new HashSet<XYZKey>();

            if (allDrains.Count <= 1)
            {
                // If only one drain, put it in both sides to ensure at least one path
                foreach (var drain in allDrains)
                {
                    leftDrains.Add(drain);
                    rightDrains.Add(drain);
                }
                return (leftDrains, rightDrains);
            }

            // Sort drains by angle from ridge point
            var sortedDrains = allDrains
                .Select(d => new { Drain = d, Angle = GetAngleFromPoint(ridgePoint, d) })
                .OrderBy(x => x.Angle)
                .ToList();

            // Split into two halves
            int half = sortedDrains.Count / 2;

            for (int i = 0; i < sortedDrains.Count; i++)
            {
                if (i < half)
                    leftDrains.Add(sortedDrains[i].Drain);
                else
                    rightDrains.Add(sortedDrains[i].Drain);
            }

            return (leftDrains, rightDrains);
        }

        // Calculate angle from point A to point B in XY plane
        private double GetAngleFromPoint(XYZKey A, XYZKey B)
        {
            double dx = B.X - A.X;
            double dy = B.Y - A.Y;
            return Math.Atan2(dy, dx);
        }
    }

    // ==================== XYZKey STRUCT ====================
    internal readonly struct XYZKey
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public XYZKey(XYZ p)
        {
            X = Math.Round(p.X, 6);
            Y = Math.Round(p.Y, 6);
            Z = Math.Round(p.Z, 6);
        }

        public XYZ ToXYZ() => new XYZ(X, Y, Z);

        public double DistanceTo(XYZKey o)
        {
            double dx = X - o.X;
            double dy = Y - o.Y;
            double dz = Z - o.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public double DistanceTo2D(XYZKey o)
        {
            double dx = X - o.X;
            double dy = Y - o.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override int GetHashCode() =>
            HashCode.Combine(X, Y, Z);

        public override bool Equals(object obj) =>
            obj is XYZKey k &&
            Math.Abs(X - k.X) < 1e-9 &&
            Math.Abs(Y - k.Y) < 1e-9 &&
            Math.Abs(Z - k.Z) < 1e-9;
    }
}