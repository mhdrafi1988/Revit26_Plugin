using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V100.Models;

namespace Revit26_Plugin.Creaser_V100.Services
{
    /// <summary>
    /// Builds a graph from slab shape crease segments and provides
    /// point snapping for corners and drains.
    /// </summary>
    public class GraphBuildService
    {
        private readonly ILogService _log;

        // Tolerance used to merge nearly coincident points (feet)
        private const double NodeTolerance = 1e-4;

        private readonly List<GraphNode> _nodes = new();
        private readonly List<GraphEdge> _edges = new();

        public IReadOnlyList<GraphNode> Nodes => _nodes;
        public IReadOnlyList<GraphEdge> Edges => _edges;

        public GraphBuildService(ILogService log)
        {
            _log = log;
        }

        // ------------------------------------------------------------
        // Build graph from crease segments
        // ------------------------------------------------------------
        public void BuildFromCreases(IEnumerable<CreaseSegment> creases)
        {
            using (_log.Scope(nameof(GraphBuildService), "BuildFromCreases"))
            {
                foreach (CreaseSegment seg in creases)
                {
                    int startId = GetOrCreateNode(seg.Start);
                    int endId = GetOrCreateNode(seg.End);

                    double weight = seg.Length;

                    // Bidirectional edges
                    _edges.Add(new GraphEdge(startId, endId, weight));
                    _edges.Add(new GraphEdge(endId, startId, weight));

                    _log.Info(nameof(GraphBuildService),
                        $"Edge added: {startId} <-> {endId}, Length={weight:F4}");
                }

                _log.Info(nameof(GraphBuildService),
                    $"Graph complete: Nodes={_nodes.Count}, Edges={_edges.Count}");
            }
        }

        // ------------------------------------------------------------
        // Node creation / merge
        // ------------------------------------------------------------
        private int GetOrCreateNode(XYZ point)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].Point.DistanceTo(point) < NodeTolerance)
                {
                    return _nodes[i].Id;
                }
            }

            int id = _nodes.Count;
            _nodes.Add(new GraphNode(id, point));

            _log.Info(nameof(GraphBuildService),
                $"Node created: Id={id}, XYZ={point}");

            return id;
        }

        // ------------------------------------------------------------
        // Snap an arbitrary point (corner / drain) to nearest node
        // ------------------------------------------------------------
        public int SnapPoint(XYZ point)
        {
            using (_log.Scope(nameof(GraphBuildService), "SnapPoint"))
            {
                if (_nodes.Count == 0)
                {
                    _log.Error(nameof(GraphBuildService),
                        "SnapPoint failed: graph has no nodes.");
                    return -1;
                }

                double minDist = double.MaxValue;
                GraphNode closest = null;

                foreach (GraphNode node in _nodes)
                {
                    double d = node.Point.DistanceTo(point);
                    if (d < minDist)
                    {
                        minDist = d;
                        closest = node;
                    }
                }

                if (closest == null)
                {
                    _log.Error(nameof(GraphBuildService),
                        "SnapPoint failed: no closest node found.");
                    return -1;
                }

                _log.Info(nameof(GraphBuildService),
                    $"Point snapped: XYZ={point} → NodeId={closest.Id}, Dist={minDist:F4}");

                return closest.Id;
            }
        }
    }
}
