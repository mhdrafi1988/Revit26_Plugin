using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V003.Services
{
    public class RoofEdgeExtractionService
    {
        public IList<Edge> CollectEdges(IList<Face> faces)
        {
            var edges = new List<Edge>();
            foreach (var f in faces)
            {
                foreach (EdgeArray loop in f.EdgeLoops)
                {
                    foreach (Edge e in loop)
                        edges.Add(e);
                }
            }
            return edges;
        }
    }
}
