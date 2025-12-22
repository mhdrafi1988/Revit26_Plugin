// ============================================================
// File: RoofGraphBuilder.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal class RoofGraphBuilder
    {
        // Tolerance used for "lowest elevation" detection (model units, feet).
        private const double DrainTol = 1e-6;

        public RoofGraphData Build(RoofBase roof)
        {
            // Undirected adjacency list
            Dictionary<XYZKey, List<XYZKey>> graph = new();

            // Collect all nodes (includes tessellated points)
            HashSet<XYZKey> allNodes = new();

            // Boundary corners = endpoints of boundary edges (unique)
            HashSet<XYZKey> boundaryCorners = new();

            Options opt = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null)
            {
                return RoofGraphData.Empty();
            }

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Faces.IsEmpty)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf)
                        continue;

                    // We only care about faces that are "roof underside-ish".
                    // This is intentionally mild to avoid excluding valid roof slopes.
                    if (pf.FaceNormal.Z >= -0.01)
                        continue;

                    foreach (EdgeArray loop in pf.EdgeLoops)
                    {
                        foreach (Edge e in loop)
                        {
                            Curve c = e.AsCurve();
                            if (c == null) continue;

                            // Boundary corners from curve endpoints (works for arcs/circles as edges are trimmed)
                            boundaryCorners.Add(new XYZKey(c.GetEndPoint(0)));
                            boundaryCorners.Add(new XYZKey(c.GetEndPoint(1)));

                            // Tessellate curve so we support arcs/circles/curved paths
                            IList<XYZ> pts = c.Tessellate();
                            if (pts == null || pts.Count < 2) continue;

                            for (int i = 0; i < pts.Count - 1; i++)
                            {
                                XYZKey a = new XYZKey(pts[i]);
                                XYZKey b = new XYZKey(pts[i + 1]);

                                allNodes.Add(a);
                                allNodes.Add(b);

                                AddUndirected(graph, a, b);
                            }
                        }
                    }
                }
            }

            // Identify drains as lowest-Z nodes across the whole graph node set
            HashSet<XYZKey> drains = new();
            int drainCandidatesCount = 0;

            if (allNodes.Count > 0)
            {
                double minZ = allNodes.Min(n => n.Z);

                // candidates = within tolerance of minZ
                foreach (var n in allNodes)
                {
                    if (Math.Abs(n.Z - minZ) <= DrainTol)
                    {
                        drains.Add(n);
                        drainCandidatesCount++;
                    }
                }
            }

            // Node index: helps map "endpoint corner keys" to actual stored keys if equal under rounding
            Dictionary<XYZKey, XYZKey> nodeIndex = new();
            foreach (var n in allNodes)
            {
                if (!nodeIndex.ContainsKey(n))
                    nodeIndex[n] = n;
            }

            return new RoofGraphData(
                graph,
                allNodes,
                boundaryCorners,
                drains,
                drainCandidatesCount,
                nodeIndex);
        }

        private static void AddUndirected(Dictionary<XYZKey, List<XYZKey>> graph, XYZKey a, XYZKey b)
        {
            AddDirected(graph, a, b);
            AddDirected(graph, b, a);
        }

        private static void AddDirected(Dictionary<XYZKey, List<XYZKey>> graph, XYZKey from, XYZKey to)
        {
            if (!graph.TryGetValue(from, out var list))
            {
                list = new List<XYZKey>();
                graph[from] = list;
            }

            // prevent duplicates
            if (!list.Contains(to))
                list.Add(to);
        }
    }
}
