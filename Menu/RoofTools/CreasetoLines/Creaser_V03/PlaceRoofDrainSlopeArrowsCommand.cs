// ============================================================
// File: PlaceRoofDrainSlopeArrowsCommand.cs
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Creaser_V03.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserCommand : IExternalCommand
    {
        private const double Z_TOL = 1e-6;
        private const double POINT_TOL = 1e-4;

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

                // ---------- 2) Ridges → full traversal ----------
                foreach (XYZKey start in boundaryNodes)
                {
                    if (cornerNodes.Contains(start)) continue;
                    if (!graph.ContainsKey(start)) continue;
                    if (graph[start].Count == 0) continue;

                    HashSet<DrainEdge> edges =
                        DrainTraversalSolver.TraverseAll(
                            start, drainNodes, graph);

                    foreach (DrainEdge edge in edges)
                        Place(edge.From, edge.To);
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
    }

    // ---------------- XYZKey ----------------
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

        public override int GetHashCode() =>
            HashCode.Combine(X, Y, Z);

        public override bool Equals(object obj) =>
            obj is XYZKey k &&
            X == k.X &&
            Y == k.Y &&
            Z == k.Z;
    }

    // Add this class to implement the missing DrainTraversalSolver
    internal static class DrainTraversalSolver
    {
        public static HashSet<DrainEdge> TraverseAll(
            XYZKey start,
            HashSet<XYZKey> drainNodes,
            Dictionary<XYZKey, List<XYZKey>> graph)
        {
            HashSet<DrainEdge> edges = new HashSet<DrainEdge>();
            HashSet<XYZKey> visited = new HashSet<XYZKey>();

            void Traverse(XYZKey current)
            {
                if (visited.Contains(current)) return;
                visited.Add(current);

                foreach (XYZKey neighbor in graph[current])
                {
                    edges.Add(new DrainEdge(current, neighbor));
                    Traverse(neighbor);
                }
            }

            Traverse(start);
            return edges;
        }
    }

    // Add this struct to represent DrainEdge
    internal readonly struct DrainEdge
    {
        public XYZKey From { get; }
        public XYZKey To { get; }

        public DrainEdge(XYZKey from, XYZKey to)
        {
            From = from;
            To = to;
        }

        public override int GetHashCode() =>
            HashCode.Combine(From, To);

        public override bool Equals(object obj) =>
            obj is DrainEdge edge &&
            From.Equals(edge.From) &&
            To.Equals(edge.To);
    }
}
