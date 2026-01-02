using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    internal class EdgeGraphNode
    {
        public XYZ Point { get; }
        public List<FlattenedEdge2D> ConnectedEdges { get; }
            = new List<FlattenedEdge2D>();

        public EdgeGraphNode(XYZ point)
        {
            Point = point;
        }
    }
}
