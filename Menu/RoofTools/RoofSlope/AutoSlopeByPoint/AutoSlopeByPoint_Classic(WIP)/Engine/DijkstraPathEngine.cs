using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Engine
{
    public class DijkstraPathEngine
    {
        private readonly List<SlabShapeVertex> _vertices;
        private readonly Dictionary<int, List<int>> _adjacencyList = new();
        private readonly Face _topFace;
        private readonly double _edgeThresholdFt;
        private readonly double _projToleranceFt;

        public DijkstraPathEngine(
            List<SlabShapeVertex> vertices,
            Face topFace,
            double edgeThresholdFt)
        {
            _vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            _topFace = topFace ?? throw new ArgumentNullException(nameof(topFace));

            // Convert edge threshold to feet if needed (should already be in feet)
            _edgeThresholdFt = edgeThresholdFt > 0 ? edgeThresholdFt : 50.0 * 0.3048; // Default ~15m

            // Projection tolerance (1mm in feet)
            _projToleranceFt = 0.00328084;

            BuildGraph();
        }

        private void BuildGraph()
        {
            int vertexCount = _vertices.Count;

            // Initialize adjacency list
            for (int i = 0; i < vertexCount; i++)
            {
                _adjacencyList[i] = new List<int>();
            }

            // Use spatial partitioning for better performance with many vertices
            var spatialMap = new Dictionary<int, List<int>>();
            double gridSize = Math.Max(1.0, _edgeThresholdFt / 2.0);

            // Build spatial grid
            for (int i = 0; i < vertexCount; i++)
            {
                XYZ pos = _vertices[i].Position;
                int gridX = (int)Math.Floor(pos.X / gridSize);
                int gridY = (int)Math.Floor(pos.Y / gridSize);
                int gridZ = (int)Math.Floor(pos.Z / gridSize);

                int gridKey = HashCode.Combine(gridX, gridY, gridZ);

                if (!spatialMap.ContainsKey(gridKey))
                    spatialMap[gridKey] = new List<int>();

                spatialMap[gridKey].Add(i);
            }

            // Build edges using spatial grid
            for (int i = 0; i < vertexCount; i++)
            {
                XYZ posA = _vertices[i].Position;

                // Get nearby grid cells
                int centerGridX = (int)Math.Floor(posA.X / gridSize);
                int centerGridY = (int)Math.Floor(posA.Y / gridSize);
                int centerGridZ = (int)Math.Floor(posA.Z / gridSize);

                // Check neighboring grid cells (3x3x3)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int gridKey = HashCode.Combine(centerGridX + dx, centerGridY + dy, centerGridZ + dz);

                            if (spatialMap.TryGetValue(gridKey, out var cellVertices))
                            {
                                foreach (int j in cellVertices)
                                {
                                    if (i >= j) continue; // Avoid duplicate edges

                                    XYZ posB = _vertices[j].Position;
                                    double distance = posA.DistanceTo(posB);

                                    // Skip if too close or beyond threshold
                                    if (distance < 0.001 || distance > _edgeThresholdFt)
                                        continue;

                                    // Check if edge lies on the face
                                    if (IsValidEdge(posA, posB))
                                    {
                                        _adjacencyList[i].Add(j);
                                        _adjacencyList[j].Add(i);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool IsValidEdge(XYZ start, XYZ end)
        {
            // For performance, sample fewer points for shorter edges
            double length = start.DistanceTo(end);
            int samples = Math.Max(3, (int)(length * 2)); // Adjust sampling density

            Line line = Line.CreateBound(start, end);

            // Check start and end points
            if (!IsPointOnFace(start) || !IsPointOnFace(end))
                return false;

            // Check intermediate points (skip first and last)
            for (int s = 1; s < samples - 1; s++)
            {
                double t = (double)s / (samples - 1);
                XYZ point = line.Evaluate(t, true);

                if (!IsPointOnFace(point))
                    return false;
            }

            return true;
        }

        private bool IsPointOnFace(XYZ point)
        {
            try
            {
                // Project point onto face
                IntersectionResult proj = _topFace.Project(point);

                // If projection fails, try with small Z offset
                if (proj == null)
                {
                    proj = _topFace.Project(point + XYZ.BasisZ * _projToleranceFt);
                    if (proj == null)
                        proj = _topFace.Project(point - XYZ.BasisZ * _projToleranceFt);
                }

                if (proj == null)
                    return false;

                UV uv = proj.UVPoint;

                try
                {
                    return _topFace.IsInside(uv);
                }
                catch
                {
                    // Fallback for complex faces
                    BoundingBoxUV bounds = _topFace.GetBoundingBox();
                    if (bounds == null)
                        return false;

                    // Allow small tolerance at boundaries
                    return uv.U >= bounds.Min.U - _projToleranceFt &&
                           uv.U <= bounds.Max.U + _projToleranceFt &&
                           uv.V >= bounds.Min.V - _projToleranceFt &&
                           uv.V <= bounds.Max.V + _projToleranceFt;
                }
            }
            catch
            {
                return false;
            }
        }

        public double ComputeShortestPath(int startVertexIndex, HashSet<int> drainVertexIndices)
        {
            if (startVertexIndex < 0 || startVertexIndex >= _vertices.Count)
                return double.PositiveInfinity;

            if (drainVertexIndices == null || drainVertexIndices.Count == 0)
                return double.PositiveInfinity;

            // Quick check: if start is a drain vertex
            if (drainVertexIndices.Contains(startVertexIndex))
                return 0.0;

            int vertexCount = _vertices.Count;
            double[] distances = new double[vertexCount];
            bool[] visited = new bool[vertexCount];

            for (int i = 0; i < vertexCount; i++)
                distances[i] = double.PositiveInfinity;

            distances[startVertexIndex] = 0.0;

            // Simple priority queue implementation for Dijkstra
            var unvisited = new List<int> { startVertexIndex };

            while (unvisited.Count > 0)
            {
                // Find unvisited vertex with smallest distance
                int current = -1;
                double minDist = double.PositiveInfinity;

                foreach (int v in unvisited)
                {
                    if (distances[v] < minDist)
                    {
                        minDist = distances[v];
                        current = v;
                    }
                }

                if (current == -1)
                    break;

                unvisited.Remove(current);

                // Check if we found a drain vertex
                if (drainVertexIndices.Contains(current))
                    return distances[current];

                visited[current] = true;

                // Update distances to neighbors
                foreach (int neighbor in _adjacencyList[current])
                {
                    if (visited[neighbor])
                        continue;

                    double edgeDist = _vertices[current].Position.DistanceTo(_vertices[neighbor].Position);
                    double newDist = distances[current] + edgeDist;

                    if (newDist < distances[neighbor])
                    {
                        distances[neighbor] = newDist;
                        if (!unvisited.Contains(neighbor))
                            unvisited.Add(neighbor);
                    }
                }
            }

            return double.PositiveInfinity;
        }

        // Get graph statistics for debugging
        public string GetGraphStats()
        {
            int totalEdges = 0;
            int minEdges = int.MaxValue;
            int maxEdges = 0;
            int isolatedVertices = 0;

            foreach (var adjList in _adjacencyList.Values)
            {
                int edgeCount = adjList.Count;
                totalEdges += edgeCount;
                minEdges = Math.Min(minEdges, edgeCount);
                maxEdges = Math.Max(maxEdges, edgeCount);

                if (edgeCount == 0)
                    isolatedVertices++;
            }

            double avgEdges = (double)totalEdges / _adjacencyList.Count;

            return $"Vertices: {_vertices.Count}, " +
                   $"Edges: {totalEdges / 2}, " +
                   $"Degree: min={minEdges}, avg={avgEdges:F1}, max={maxEdges}, " +
                   $"Isolated: {isolatedVertices}, " +
                   $"Threshold: {_edgeThresholdFt:F2}ft";
        }
    }
}