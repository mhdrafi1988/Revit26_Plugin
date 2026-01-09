using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit22_Plugin.RoofTagV4.Models;

namespace Revit22_Plugin.RoofTagV4.Services
{
    public static class RoofGeometryServiceV4
    {
        // =====================================================================
        // MAIN ENTRY: Build packaged geometry model for tagging
        // =====================================================================
        public static RoofLoopsModel BuildRoofGeometry(RoofBase roof)
        {
            RoofLoopsModel model = new RoofLoopsModel();

            // 1. Get ALL slab-shape vertices (PATCHED)
            model.AllVertices = GetShapeVertices(roof);

            // 2. Centroid (XY only)
            model.Centroid = ComputeCentroid(model.AllVertices);

            // 3. Boundary loop (outer loop of top face)
            model.Boundary = ExtractBoundaryLoop(roof);

            model.MainAxis = XYZ.BasisX; // unused but kept for compatibility

            return model;
        }

        // =====================================================================
        // GET SHAPE EDITOR VERTICES  (PATCHED)
        // =====================================================================
        private static List<XYZ> GetShapeVertices(RoofBase roof)
        {
            List<XYZ> pts = new List<XYZ>();

            SlabShapeEditor shapeEditor = roof.GetSlabShapeEditor();
            if (shapeEditor == null || !shapeEditor .IsEnabled)
                return pts;

            SlabShapeVertexArray arr = shapeEditor.SlabShapeVertices;

            int count = arr.Size;
            for (int i = 0; i < count; i++)
            {
                SlabShapeVertex sv = arr.get_Item(i);
                pts.Add(sv.Position);
            }

            return pts;
        }

        // =====================================================================
        // XY CENTROID
        // =====================================================================
        private static XYZ ComputeCentroid(List<XYZ> pts)
        {
            if (pts == null || pts.Count == 0)
                return XYZ.Zero;

            double x = pts.Average(p => p.X);
            double y = pts.Average(p => p.Y);
            double z = pts.Average(p => p.Z);

            return new XYZ(x, y, z);
        }

        // =====================================================================
        // EXTRACT OUTER BOUNDARY LOOP OF THE ROOF
        // =====================================================================
        private static List<XYZ> ExtractBoundaryLoop(RoofBase roof)
        {
            List<XYZ> boundary = new List<XYZ>();

            Options opt = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false,
                ComputeReferences = false
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return boundary;

            foreach (GeometryObject obj in geom)
            {
                Solid solid = obj as Solid;
                if (solid == null || solid.Faces.Size == 0) continue;

                foreach (Face face in solid.Faces)
                {
                    PlanarFace pf = face as PlanarFace;
                    if (pf == null) continue;
                    if (pf.FaceNormal.Z <= 0.7) continue;  // top face only

                    EdgeArrayArray loops = pf.EdgeLoops;
                    if (loops.Size == 0) continue;

                    EdgeArray loop = loops.get_Item(0); // outer loop

                    foreach (Edge e in loop)
                    {
                        IList<XYZ> tess = e.Tessellate();
                        foreach (XYZ p in tess)
                        {
                            boundary.Add(new XYZ(p.X, p.Y, p.Z));
                        }
                    }

                    return boundary; // only need the outer loop
                }
            }

            return boundary;
        }
    }
}
