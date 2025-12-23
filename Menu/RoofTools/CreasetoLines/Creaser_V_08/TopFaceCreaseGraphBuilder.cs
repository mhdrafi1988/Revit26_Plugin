using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers
{
    /// <summary>
    /// Builds a graph representation of crease candidates.
    /// </summary>
    public static class TopFaceCreaseGraphBuilder
    {
        public static IDictionary<XYZ, List<XYZ>> BuildGraph(
            IList<XYZ> corners,
            IList<XYZ> drains)
        {
            Dictionary<XYZ, List<XYZ>> graph = new();

            foreach (XYZ c in corners)
                graph[c] = new List<XYZ>();

            foreach (XYZ d in drains)
                graph[d] = new List<XYZ>();

            foreach (XYZ c in corners)
            {
                foreach (XYZ d in drains)
                {
                    graph[c].Add(d);
                    graph[d].Add(c);
                }
            }

            return graph;
        }
    }
}
