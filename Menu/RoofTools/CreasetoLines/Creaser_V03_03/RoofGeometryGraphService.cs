using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V03_03.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V03_03.Services
{
    public static class RoofGeometryGraphService
    {
        public static Dictionary<XYZKey, List<XYZKey>> BuildGraph(RoofBase roof)
        {
            var graph = new Dictionary<XYZKey, List<XYZKey>>();

            Options opt = new()
            {
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = false
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return graph;

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid) continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf) continue;
                    if (pf.FaceNormal.Z < 0.9) continue; // top face only

                    Mesh mesh = face.Triangulate();

                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        MeshTriangle t = mesh.get_Triangle(i);

                        XYZKey a = new(t.get_Vertex(0));
                        XYZKey b = new(t.get_Vertex(1));
                        XYZKey c = new(t.get_Vertex(2));

                        AddEdge(graph, a, b);
                        AddEdge(graph, b, c);
                        AddEdge(graph, c, a);

                        AddEdge(graph, b, a);
                        AddEdge(graph, c, b);
                        AddEdge(graph, a, c);
                    }
                }
            }

            return graph;
        }

        private static void AddEdge(
            Dictionary<XYZKey, List<XYZKey>> g,
            XYZKey a,
            XYZKey b)
        {
            if (!g.ContainsKey(a))
                g[a] = new List<XYZKey>();

            if (!g[a].Contains(b))
                g[a].Add(b);
        }
    }
}
