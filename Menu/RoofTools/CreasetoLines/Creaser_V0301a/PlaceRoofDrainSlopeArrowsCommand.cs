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

        private readonly Dictionary<XYZKey, XYZKey> _snapCache =
            new Dictionary<XYZKey, XYZKey>();

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;

            if (view.ViewType != ViewType.FloorPlan || view.SketchPlane == null)
            {
                message = "Run only in a plan view.";
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

            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geom = roof.get_Geometry(opt);

            Dictionary<XYZKey, List<XYZKey>> graph = new();
            Dictionary<XYZKey, int> boundaryDegree = new();

            XYZKey Snap(XYZKey p)
            {
                foreach (var e in _snapCache.Keys)
                    if (e.DistanceTo(p) < POINT_TOL)
                        return e;

                _snapCache[p] = p;
                return p;
            }

            void Ensure(XYZKey p)
            {
                if (!graph.ContainsKey(p))
                    graph[p] = new List<XYZKey>();
            }

            void AddDownhill(XYZKey a, XYZKey b)
            {
                double dz = a.Z - b.Z;
                if (dz > Z_TOL)
                {
                    Ensure(a); Ensure(b);
                    graph[a].Add(b);
                }
                else if (-dz > Z_TOL)
                {
                    Ensure(a); Ensure(b);
                    graph[b].Add(a);
                }
            }

            // ---- build graph ----
            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid) continue;

                foreach (Face f in solid.Faces)
                {
                    if (f is not PlanarFace pf) continue;
                    if (pf.FaceNormal.Z < 0.99) continue;

                    bool outer = true;
                    foreach (EdgeArray loop in pf.EdgeLoops)
                    {
                        var pts = loop
                            .Cast<Edge>()
                            .Select(e => Snap(new XYZKey(e.AsCurve().GetEndPoint(0))))
                            .ToList();

                        if (outer)
                        {
                            foreach (var p in pts)
                                boundaryDegree[p] = boundaryDegree.TryGetValue(p, out int d) ? d + 1 : 1;
                            outer = false;
                        }

                        for (int i = 0; i < pts.Count; i++)
                            AddDownhill(pts[i], pts[(i + 1) % pts.Count]);
                    }
                }
            }

            var cornerNodes = boundaryDegree
                .Where(kv => kv.Value == 2)
                .Select(kv => kv.Key)
                .ToHashSet();

            double minZ = graph.Keys.Min(p => p.Z);
            var drainNodes = graph.Keys
                .Where(p => Math.Abs(p.Z - minZ) < Z_TOL)
                .ToHashSet();

            Plane plane = view.SketchPlane.GetPlane();
            double minLen = doc.Application.ShortCurveTolerance;
            HashSet<string> placed = new();

            using Transaction tx = new(doc, "Drain Paths");
            tx.Start();

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            // 🔑 CORNERS → nearest XY drain → shortest XY downhill path
            foreach (XYZKey corner in cornerNodes)
            {
                if (!graph.ContainsKey(corner)) continue;

                XYZKey nearestDrain =
                    drainNodes.OrderBy(d => corner.DistanceTo2D(d)).First();

                var path = DrainPathSolver.FindShortestPath(
                    corner,
                    new HashSet<XYZKey> { nearestDrain },
                    graph);

                for (int i = 0; i < path.Count - 1; i++)
                    Place(path[i], path[i + 1]);
            }

            tx.Commit();
            return Result.Succeeded;

            void Place(XYZKey a, XYZKey b)
            {
                XYZ p1 = Project(a.ToXYZ(), plane);
                XYZ p2 = Project(b.ToXYZ(), plane);
                if (p1.DistanceTo(p2) < minLen) return;

                string key = $"{p1.X:F5}_{p1.Y:F5}|{p2.X:F5}_{p2.Y:F5}";
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

    internal readonly struct XYZKey
    {
        public readonly double X, Y, Z;

        public XYZKey(XYZ p)
        {
            X = Math.Round(p.X, 6);
            Y = Math.Round(p.Y, 6);
            Z = Math.Round(p.Z, 6);
        }

        public XYZ ToXYZ() => new(X, Y, Z);

        public double DistanceTo(XYZKey o) =>
            Math.Sqrt((X - o.X) * (X - o.X) +
                      (Y - o.Y) * (Y - o.Y) +
                      (Z - o.Z) * (Z - o.Z));

        public double DistanceTo2D(XYZKey o) =>
            Math.Sqrt((X - o.X) * (X - o.X) +
                      (Y - o.Y) * (Y - o.Y));

        public override int GetHashCode() =>
            HashCode.Combine(X, Y, Z);

        public override bool Equals(object obj) =>
            obj is XYZKey k &&
            Math.Abs(X - k.X) < 1e-9 &&
            Math.Abs(Y - k.Y) < 1e-9 &&
            Math.Abs(Z - k.Z) < 1e-9;
    }
}
