// ============================================================
// File: CreaseGraph.cs
// ============================================================

using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal class CreaseGraph
    {
        public Dictionary<XYZKey, List<XYZKey>> Graph { get; } = new();
        public HashSet<XYZKey> Drains { get; } = new();

        public void BuildFromRoof(RoofBase roof)
        {
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geom = roof.get_Geometry(opt);

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Faces.IsEmpty)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf)
                        continue;

                    // Only downward-facing roof faces
                    if (pf.FaceNormal.Z >= -0.01)
                        continue;

                    foreach (EdgeArray loop in pf.EdgeLoops)
                    {
                        foreach (Edge e in loop)
                        {
                            XYZ p0 = e.AsCurve().GetEndPoint(0);
                            XYZ p1 = e.AsCurve().GetEndPoint(1);

                            XYZKey a = new XYZKey(p0);
                            XYZKey b = new XYZKey(p1);

                            // UNDIRECTED connectivity
                            AddEdge(a, b);
                            AddEdge(b, a);
                        }
                    }
                }
            }

            IdentifyDrains();
        }

        private void AddEdge(XYZKey from, XYZKey to)
        {
            if (!Graph.TryGetValue(from, out var list))
            {
                list = new List<XYZKey>();
                Graph[from] = list;
            }

            if (!list.Contains(to))
                list.Add(to);
        }

        /// <summary>
        /// Drains = lowest Z vertices (true sinks)
        /// </summary>
        private void IdentifyDrains()
        {
            if (!Graph.Any())
                return;

            double minZ = Graph.Keys.Min(k => k.Z);

            foreach (var k in Graph.Keys)
            {
                if (System.Math.Abs(k.Z - minZ) < 1e-6)
                    Drains.Add(k);
            }
        }
    }
}
