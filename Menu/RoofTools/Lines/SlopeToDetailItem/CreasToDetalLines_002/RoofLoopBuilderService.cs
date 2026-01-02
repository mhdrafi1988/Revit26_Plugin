using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofLoopBuilderService
    {
        public IList<EdgeLoop2D> BuildLoops(
            IList<FlattenedEdge2D> edges,
            LoggingService log)
        {
            if (edges == null || edges.Count == 0)
                throw new ArgumentException("No edges provided.");

            log.Info("Building 2D edge graph.");

            var comparer = new Point2DComparer();
            var nodes = new Dictionary<XYZ, EdgeGraphNode>(comparer);

            foreach (FlattenedEdge2D e in edges)
            {
                AddNode(e.Start2D, e, nodes);
                AddNode(e.End2D, e, nodes);
            }

            foreach (EdgeGraphNode node in nodes.Values)
            {
                if (node.ConnectedEdges.Count < 2)
                {
                    log.Error($"Open edge detected at {node.Point}.");
                    throw new InvalidOperationException("Open edges found. Geometry invalid.");
                }
            }

            log.Info("Edge graph validated. Building loops.");

            var unused = new HashSet<FlattenedEdge2D>(edges);
            var loops = new List<EdgeLoop2D>();

            while (unused.Any())
            {
                FlattenedEdge2D seed = unused.First();
                EdgeLoop2D loop = TraceClosedLoop(seed, unused, nodes, comparer, log);

                if (loop.Edges.Count < 3)
                {
                    log.Error("Degenerate loop detected.");
                    throw new InvalidOperationException("Invalid loop geometry.");
                }

                loops.Add(loop);
            }

            log.Info($"Closed loops built: {loops.Count}");
            return loops;
        }

        private static void AddNode(
            XYZ point,
            FlattenedEdge2D edge,
            Dictionary<XYZ, EdgeGraphNode> nodes)
        {
            if (!nodes.TryGetValue(point, out EdgeGraphNode node))
            {
                node = new EdgeGraphNode(point);
                nodes.Add(point, node);
            }

            node.ConnectedEdges.Add(edge);
        }

        private static EdgeLoop2D TraceClosedLoop(
            FlattenedEdge2D seed,
            HashSet<FlattenedEdge2D> unused,
            Dictionary<XYZ, EdgeGraphNode> nodes,
            IEqualityComparer<XYZ> comparer,
            LoggingService log)
        {
            var loop = new EdgeLoop2D();

            XYZ startPoint = seed.Start2D;
            XYZ currentPoint = seed.End2D;

            loop.Edges.Add(seed);
            unused.Remove(seed);

            FlattenedEdge2D prevEdge = seed;

            int guard = 0;
            while (guard++ < 100000)
            {
                if (comparer.Equals(currentPoint, startPoint))
                    break;

                EdgeGraphNode node = nodes.First(k => comparer.Equals(k.Key, currentPoint)).Value;

                FlattenedEdge2D nextRaw =
                    node.ConnectedEdges
                        .FirstOrDefault(e => unused.Contains(e));

                if (nextRaw == null)
                {
                    log.Error($"Loop tracing stopped early at {currentPoint}.");
                    throw new InvalidOperationException("Loop tracing failed (open traversal).");
                }

                FlattenedEdge2D nextOriented = OrientEdgeFrom(nextRaw, currentPoint, comparer);

                loop.Edges.Add(nextOriented);
                unused.Remove(nextRaw);

                prevEdge = nextRaw;
                currentPoint = nextOriented.End2D;
            }

            if (!comparer.Equals(currentPoint, startPoint))
                throw new InvalidOperationException("Loop did not close.");

            return loop;
        }

        private static FlattenedEdge2D OrientEdgeFrom(
            FlattenedEdge2D edge,
            XYZ fromPoint,
            IEqualityComparer<XYZ> comparer)
        {
            if (comparer.Equals(edge.Start2D, fromPoint))
                return edge;

            if (comparer.Equals(edge.End2D, fromPoint))
                return new FlattenedEdge2D(edge.End2D, edge.Start2D);

            throw new InvalidOperationException("Edge does not connect to expected node.");
        }
    }
}
