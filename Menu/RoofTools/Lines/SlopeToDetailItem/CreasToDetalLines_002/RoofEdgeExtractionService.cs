using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofEdgeExtractionService
    {
        public IList<Edge> CollectEdges(IEnumerable<Face> topFaces)
        {
            var edges = new List<Edge>();

            foreach (Face face in topFaces)
            {
                foreach (EdgeArray loop in face.EdgeLoops)
                {
                    foreach (Edge edge in loop)
                        edges.Add(edge);
                }
            }

            return edges;
        }
    }
}
