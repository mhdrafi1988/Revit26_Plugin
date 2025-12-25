// ============================================================
// File: PlaceRoofCreaseDetailLinesCommand.cs
// Revit Version: 2026
// Namespace: Revit26_Plugin.Creaser_V100.Commands
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Creaser_A100.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceRoofCreaseDetailLinesCommand : IExternalCommand
    {
        private const double NORMAL_ANGLE_TOL = 1e-3;
        private const double MIN_LENGTH_TOL = 1e-4;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            View view = doc?.ActiveView;

            if (doc == null || view == null)
            {
                message = "Invalid Revit context.";
                return Result.Failed;
            }

            if (view.ViewType != ViewType.FloorPlan || view.SketchPlane == null)
            {
                message = "Command must be run in a plan view.";
                return Result.Failed;
            }

            RoofBase roof = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofBase))
                .Cast<RoofBase>()
                .FirstOrDefault(r =>
                {
                    try { return r.GetSlabShapeEditor()?.IsEnabled == true; }
                    catch { return false; }
                });

            if (roof == null)
            {
                message = "No shape-edited roof found.";
                return Result.Failed;
            }

            Options options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = roof.get_Geometry(options);
            if (geom == null)
            {
                message = "Failed to read roof geometry.";
                return Result.Failed;
            }

            List<PlanarFace> topFaces = new List<PlanarFace>();

            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.FaceNormal.Z > 0.9)
                            topFaces.Add(pf);
                    }
                }
            }

            if (topFaces.Count < 2)
            {
                message = "Not enough roof facets.";
                return Result.Failed;
            }

            Dictionary<string, List<PlanarFace>> edgeMap = new();

            foreach (PlanarFace face in topFaces)
            {
                foreach (EdgeArray loop in face.EdgeLoops)
                {
                    foreach (Edge edge in loop)
                    {
                        string key = GetEdgeKey(edge.AsCurve());

                        if (!edgeMap.ContainsKey(key))
                            edgeMap[key] = new List<PlanarFace>();

                        edgeMap[key].Add(face);
                    }
                }
            }

            List<Curve> creaseCurves = new();

            foreach (var kvp in edgeMap)
            {
                if (kvp.Value.Count != 2)
                    continue;

                XYZ n1 = kvp.Value[0].FaceNormal.Normalize();
                XYZ n2 = kvp.Value[1].FaceNormal.Normalize();

                if (n1.AngleTo(n2) > NORMAL_ANGLE_TOL)
                {
                    Curve c = GetCurveFromKey(kvp.Key);
                    if (c != null && c.Length > MIN_LENGTH_TOL)
                        creaseCurves.Add(c);
                }
            }

            if (!creaseCurves.Any())
            {
                message = "No creases detected.";
                return Result.Failed;
            }

            Plane plane = view.SketchPlane.GetPlane();

            using (Transaction tx = new Transaction(doc, "Place Roof Crease Detail Lines"))
            {
                tx.Start();

                foreach (Curve c in creaseCurves)
                {
                    XYZ p0 = ProjectToPlane(c.GetEndPoint(0), plane);
                    XYZ p1 = ProjectToPlane(c.GetEndPoint(1), plane);

                    if (p0.DistanceTo(p1) < MIN_LENGTH_TOL)
                        continue;

                    doc.Create.NewDetailCurve(view, Line.CreateBound(p0, p1));
                }

                tx.Commit();
            }

            return Result.Succeeded;
        }

        private static string GetEdgeKey(Curve c)
        {
            XYZ a = c.GetEndPoint(0);
            XYZ b = c.GetEndPoint(1);

            string p1 = $"{Math.Round(a.X, 6)}_{Math.Round(a.Y, 6)}_{Math.Round(a.Z, 6)}";
            string p2 = $"{Math.Round(b.X, 6)}_{Math.Round(b.Y, 6)}_{Math.Round(b.Z, 6)}";

            return string.CompareOrdinal(p1, p2) < 0 ? $"{p1}|{p2}" : $"{p2}|{p1}";
        }

        private static Curve GetCurveFromKey(string key)
        {
            string[] parts = key.Split('|');
            if (parts.Length != 2) return null;

            XYZ p0 = Parse(parts[0]);
            XYZ p1 = Parse(parts[1]);
            return Line.CreateBound(p0, p1);
        }

        private static XYZ Parse(string s)
        {
            var p = s.Split('_');
            return new XYZ(
                double.Parse(p[0]),
                double.Parse(p[1]),
                double.Parse(p[2]));
        }

        private static XYZ ProjectToPlane(XYZ p, Plane plane)
        {
            XYZ v = p - plane.Origin;
            return p - v.DotProduct(plane.Normal) * plane.Normal;
        }
    }
}
