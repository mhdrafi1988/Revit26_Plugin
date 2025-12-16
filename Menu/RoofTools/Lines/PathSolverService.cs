using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public class PathSolverService
    {
        // =====================================================
        // DISTANCE MAP (used by ridge detection)
        // =====================================================
        public Dictionary<SlabShapeVertex, double> ComputeDistanceToNearestDrain(
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
            HashSet<SlabShapeVertex> drainVertices)
        {
            var result = new Dictionary<SlabShapeVertex, double>();

            foreach (var v in graph.Keys)
            {
                double min = double.PositiveInfinity;

                foreach (var d in drainVertices)
                {
                    double dist = DijkstraDistance(graph, v, d);
                    if (dist < min)
                        min = dist;
                }

                if (!double.IsInfinity(min))
                    result[v] = min;
            }

            return result;
        }

        // =====================================================
        // SHORTEST PATH TO NEAREST DRAIN
        // =====================================================
        public List<XYZ> FindPathToNearestDrain(
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
            SlabShapeVertex start,
            HashSet<SlabShapeVertex> drainVertices,
            out double pathLength)
        {
            pathLength = double.PositiveInfinity;

            SlabShapeVertex bestDrain = null;
            Dictionary<SlabShapeVertex, SlabShapeVertex> bestPrev = null;

            foreach (var drain in drainVertices)
            {
                var prev = new Dictionary<SlabShapeVertex, SlabShapeVertex>();
                double dist = Dijkstra(graph, start, drain, prev);

                if (dist < pathLength)
                {
                    pathLength = dist;
                    bestDrain = drain;
                    bestPrev = prev;
                }
            }

            if (bestDrain == null || bestPrev == null)
                return null;

            return ReconstructPath(bestPrev, bestDrain);
        }

        // =====================================================
        // CORE DIJKSTRA (distance only)
        // =====================================================
        private double DijkstraDistance(
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
            SlabShapeVertex start,
            SlabShapeVertex end)
        {
            var dist = new Dictionary<SlabShapeVertex, double>();
            var visited = new HashSet<SlabShapeVertex>();

            foreach (var v in graph.Keys)
                dist[v] = double.PositiveInfinity;

            dist[start] = 0;

            while (true)
            {
                SlabShapeVertex current = null;
                double min = double.PositiveInfinity;

                foreach (var kv in dist)
                {
                    if (!visited.Contains(kv.Key) && kv.Value < min)
                    {
                        min = kv.Value;
                        current = kv.Key;
                    }
                }

                if (current == null)
                    break;

                if (current == end)
                    return min;

                visited.Add(current);

                foreach (var n in graph[current])
                {
                    double alt =
                        dist[current] +
                        current.Position.DistanceTo(n.Position);

                    if (alt < dist[n])
                        dist[n] = alt;
                }
            }

            return double.PositiveInfinity;
        }

        // =====================================================
        // CORE DIJKSTRA (with path reconstruction)
        // =====================================================
        private double Dijkstra(
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
            SlabShapeVertex start,
            SlabShapeVertex end,
            Dictionary<SlabShapeVertex, SlabShapeVertex> prev)
        {
            var dist = new Dictionary<SlabShapeVertex, double>();
            var visited = new HashSet<SlabShapeVertex>();

            foreach (var v in graph.Keys)
            {
                dist[v] = double.PositiveInfinity;
                prev[v] = null;
            }

            dist[start] = 0;

            while (true)
            {
                SlabShapeVertex current = null;
                double min = double.PositiveInfinity;

                foreach (var kv in dist)
                {
                    if (!visited.Contains(kv.Key) && kv.Value < min)
                    {
                        min = kv.Value;
                        current = kv.Key;
                    }
                }

                if (current == null)
                    break;

                if (current == end)
                    return min;

                visited.Add(current);

                foreach (var n in graph[current])
                {
                    double alt =
                        dist[current] +
                        current.Position.DistanceTo(n.Position);

                    if (alt < dist[n])
                    {
                        dist[n] = alt;
                        prev[n] = current;
                    }
                }
            }

            return double.PositiveInfinity;
        }

        // =====================================================
        // PATH RECONSTRUCTION
        // =====================================================
        private List<XYZ> ReconstructPath(
            Dictionary<SlabShapeVertex, SlabShapeVertex> prev,
            SlabShapeVertex end)
        {
            var path = new List<XYZ>();
            var current = end;

            while (current != null)
            {
                path.Insert(0, current.Position);
                current = prev[current];
            }

            return path.Count > 1 ? path : null;
        }
    }
}
