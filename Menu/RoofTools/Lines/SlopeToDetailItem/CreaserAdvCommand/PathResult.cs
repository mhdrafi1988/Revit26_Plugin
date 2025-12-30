using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_adv_V001.Models
{
    /// <summary>
    /// Result of a pathfinding operation.
    /// </summary>
    public class PathResult
    {
        public bool PathFound { get; set; }

        public GraphNode StartNode { get; set; }

        public GraphNode EndNode { get; set; }

        public IList<GraphNode> OrderedNodes { get; set; } = new List<GraphNode>();

        public double TotalLength { get; set; }

        public string FailureReason { get; set; }
    }
}
