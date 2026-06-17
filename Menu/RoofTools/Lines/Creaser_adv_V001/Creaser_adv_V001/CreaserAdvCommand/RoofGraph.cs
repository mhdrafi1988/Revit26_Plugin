using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_adv_V001.Models
{
    public class RoofGraph
    {
        public IList<GraphNode> Nodes { get; } = new List<GraphNode>();
        public IList<GraphNode> CornerNodes { get; } = new List<GraphNode>();
        public IList<GraphNode> DrainNodes { get; } = new List<GraphNode>();
    }
}
