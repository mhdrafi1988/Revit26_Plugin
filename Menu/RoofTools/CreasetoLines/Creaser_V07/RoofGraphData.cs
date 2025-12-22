// ============================================================
// File: RoofGraphData.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    internal sealed class RoofGraphData
    {
        public Dictionary<XYZKey, List<XYZKey>> Graph { get; }
        public IReadOnlyCollection<XYZKey> AllNodes { get; }
        public IReadOnlyCollection<XYZKey> BoundaryCorners { get; } // Point Set-01
        public IReadOnlyCollection<XYZKey> Drains { get; }          // Point Set-02

        public int AllNodesCount => AllNodes.Count;
        public int DrainCandidatesCount { get; }
        public Dictionary<XYZKey, XYZKey> NodeIndex { get; }

        public RoofGraphData(
            Dictionary<XYZKey, List<XYZKey>> graph,
            IReadOnlyCollection<XYZKey> allNodes,
            IReadOnlyCollection<XYZKey> boundaryCorners,
            IReadOnlyCollection<XYZKey> drains,
            int drainCandidatesCount,
            Dictionary<XYZKey, XYZKey> nodeIndex)
        {
            Graph = graph;
            AllNodes = allNodes;
            BoundaryCorners = boundaryCorners;
            Drains = drains;
            DrainCandidatesCount = drainCandidatesCount;
            NodeIndex = nodeIndex;
        }

        public static RoofGraphData Empty()
        {
            return new RoofGraphData(
                new Dictionary<XYZKey, List<XYZKey>>(),
                new List<XYZKey>(),
                new List<XYZKey>(),
                new List<XYZKey>(),
                0,
                new Dictionary<XYZKey, XYZKey>());
        }
    }
}
