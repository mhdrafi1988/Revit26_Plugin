using Autodesk.Revit.DB;
using Revit22_Plugin.V4_02.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.V4_02.Domain.Services
{
    public class DrainDetectionService
    {
        public List<DrainItem> DetectDrainsFromRoof(RoofBase roof, Face ignoredTopFace)
        {
            var drains = new List<DrainItem>();

            try
            {
                drains.AddRange(DetectFromGeometryLoops(roof));
                drains.AddRange(DetectFromOpeningElements(roof));
                drains = RemoveDuplicateDrains(drains);

                return drains
                    .OrderBy(d => d.Width * d.Height)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Drain detection failed: {ex.Message}");
            }
        }

        // =========================================================
        // A) SOLID GEOMETRY (INNER LOOPS)
        // =========================================================
        private List<DrainItem> DetectFromGeometryLoops(RoofBase roof)
        {
            var drains = new List<DrainItem>();

            Options opt = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return drains;

            foreach (GeometryObject go in geom)
            {
                if (go is not Solid solid || solid.Faces.Size == 0)
                    continue;

                foreach (Face face in GetUpwardFaces(solid, 0.95))
                {
                    int outerIndex = FindOuterLoop(face);
                    if (outerIndex < 0) continue;

                    for (int i = 0; i < face.EdgeLoops.Size; i++)
                    {
                        if (i == outerIndex) continue;

                        var loop = face.EdgeLoops.get_Item(i);
                        var drain = CreateDrainFromLoop(loop, face);
                        if (drain != null)
                            drains.Add(drain);
                    }
                }
            }

            return drains;
        }

        private int FindOuterLoop(Face face)
        {
            int outerIndex = -1;
            double maxPerimeter = 0;

            for (int i = 0; i < face.EdgeLoops.Size; i++)
            {
                var loop = face.EdgeLoops.get_Item(i);
                double perimeter = 0;

                foreach (Edge e in loop)
                {
                    Curve c = e.AsCurve();
                    if (c != null)
                        perimeter += c.Length;
                }

                if (perimeter > maxPerimeter)
                {
                    maxPerimeter = perimeter;
                    outerIndex = i;
                }
            }

            return outerIndex;
        }

        // =========================================================
        // B) REVIT OPENING ELEMENTS
        // =========================================================
        private List<DrainItem> DetectFromOpeningElements(RoofBase roof)
        {
            var doc = roof.Document;
            var drains = new List<DrainItem>();

            var openings = new FilteredElementCollector(doc)
                .OfClass(typeof(Opening))
                .Cast<Opening>()
                .Where(o => o.Host?.Id == roof.Id)
                .ToList();

            foreach (var op in openings)
            {
                try
                {
                    CurveArray arr = op.BoundaryCurves;
                    if (arr == null || arr.Size < 2) continue;

                    var pts = new List<XYZ>();
                    var arcs = new List<Arc>();
                    var curves = new List<Curve>();

                    foreach (Curve c in arr)
                    {
                        if (c is Arc a) arcs.Add(a);
                        curves.Add(c);
                        pts.Add(c.GetEndPoint(0));
                        pts.Add(c.GetEndPoint(1));
                    }

                    var drain = ClassifyOpeningShape(pts, arcs, curves, op.Id);
                    if (drain != null)
                        drains.Add(drain);
                }
                catch { }
            }

            return drains;
        }

        private DrainItem CreateDrainFromLoop(EdgeArray loop, Face face)
        {
            var pts = new List<XYZ>();
            var arcs = new List<Arc>();
            var curves = new List<Curve>();

            foreach (Edge e in loop)
            {
                Curve c = e.AsCurve();
                if (c == null) continue;

                curves.Add(c);
                if (c is Arc arc) arcs.Add(arc);

                pts.Add(face.Project(c.GetEndPoint(0)).XYZPoint);
                pts.Add(face.Project(c.GetEndPoint(1)).XYZPoint);
            }

            return ClassifyOpeningShape(pts, arcs, curves, null);
        }

        // =========================================================
        // SHAPE CLASSIFICATION
        // =========================================================
        private DrainItem ClassifyOpeningShape(
            List<XYZ> pts,
            List<Arc> arcs,
            List<Curve> curves,
            ElementId id)
        {
            if (pts.Count < 4) return null;

            BoundingDims(pts, out XYZ center, out double w, out double h);
            string shape =
                Math.Abs(w - h) < 5 ? "Square" :
                curves.All(c => c is Line) ? "Rectangle" :
                arcs.Count > 0 ? "Circle" : "Other";

            return new DrainItem(center, w, h, shape, id);
        }

        private IEnumerable<Face> GetUpwardFaces(Solid solid, double minZ)
        {
            foreach (Face f in solid.Faces)
            {
                XYZ n = f.ComputeNormal(new UV(0.5, 0.5))?.Normalize();
                if (n != null && n.Z >= minZ)
                    yield return f;
            }
        }

        private void BoundingDims(List<XYZ> pts, out XYZ c, out double w, out double h)
        {
            double minX = pts.Min(p => p.X);
            double maxX = pts.Max(p => p.X);
            double minY = pts.Min(p => p.Y);
            double maxY = pts.Max(p => p.Y);
            double minZ = pts.Min(p => p.Z);
            double maxZ = pts.Max(p => p.Z);

            c = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            w = (maxX - minX) * 304.8;
            h = (maxY - minY) * 304.8;
        }

        private List<DrainItem> RemoveDuplicateDrains(List<DrainItem> drains)
        {
            var list = new List<DrainItem>();
            foreach (var d in drains)
            {
                if (!list.Any(x => x.CenterPoint.DistanceTo(d.CenterPoint) < 0.01))
                    list.Add(d);
            }
            return list;
        }
    }
}
