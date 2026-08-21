using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Geometry
{
    /// <summary>
    /// Clusters curve endpoints that fall within tolerance of each other into
    /// shared nodes, so downstream steps (snap, gap-close, loop assembly) can
    /// reason about the curve set as a graph instead of raw XYZ pairs.
    /// Node coordinate is the first endpoint seen in a cluster ("snap to
    /// first"), which is equivalent to a centroid within the tolerance band.
    /// </summary>
    public class EndpointGraph
    {
        public List<XYZ> Nodes { get; } = new();
        public List<(int Start, int End)> Edges { get; } = new();

        public static EndpointGraph Build(IList<Curve> curves, double tolerance)
        {
            var graph = new EndpointGraph();

            int FindOrAdd(XYZ p)
            {
                for (int k = 0; k < graph.Nodes.Count; k++)
                {
                    if (graph.Nodes[k].DistanceTo(p) <= tolerance)
                        return k;
                }
                graph.Nodes.Add(p);
                return graph.Nodes.Count - 1;
            }

            foreach (Curve c in curves)
            {
                int a = FindOrAdd(c.GetEndPoint(0));
                int b = FindOrAdd(c.GetEndPoint(1));
                graph.Edges.Add((a, b));
            }

            return graph;
        }

        public int Degree(int node)
        {
            int count = 0;
            foreach (var e in Edges)
            {
                if (e.Start == node || e.End == node)
                    count++;
            }
            return count;
        }
    }
}
