using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_adv_V001.Models
{
    public class PathResult
    {
        public bool PathFound { get; }
        public IList<GraphNode> OrderedNodes { get; }
        public GraphNode EndNode { get; }
        public string FailureReason { get; }

        public PathResult(
            IList<GraphNode> orderedNodes,
            GraphNode endNode,
            bool pathFound,
            string failureReason)
        {
            OrderedNodes = orderedNodes;
            EndNode = endNode;
            PathFound = pathFound;
            FailureReason = failureReason;
        }
    }
}
