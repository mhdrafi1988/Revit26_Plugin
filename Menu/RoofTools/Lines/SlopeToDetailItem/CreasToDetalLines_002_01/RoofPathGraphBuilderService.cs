using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofPathGraphBuilderService
    {
        public Dictionary<XYZ, List<XYZ>> BuildGraph(
            IList<FlattenedEdge2D> edges)
        {
            var comparer = new Point2DComparer();
            var graph = new Dictionary<XYZ, List<XYZ>>(comparer);

            foreach (var e in edges)
            {
                Add(graph, e.Start2D, e.End2D, comparer);
                Add(graph, e.End2D, e.Start2D, comparer);
            }

            return graph;
        }

        private static void Add(
            Dictionary<XYZ, List<XYZ>> graph,
            XYZ from,
            XYZ to,
            IEqualityComparer<XYZ> comparer)
        {
            if (!graph.TryGetValue(from, out var list))
            {
                list = new List<XYZ>();
                graph[from] = list;
            }

            if (!list.Contains(to, comparer))
                list.Add(to);
        }
    }
}
