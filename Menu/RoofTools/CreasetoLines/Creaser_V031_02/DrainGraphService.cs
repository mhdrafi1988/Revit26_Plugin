using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V31.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V31.Services
{
    /// <summary>
    /// Service that builds a drainage graph from roof geometry data.
    /// Classifies nodes into corners, ridges, drains, and other boundaries.
    /// </summary>
    public class DrainGraphService
    {
        /// <summary>
        /// Converts raw roof geometry into a classified drainage graph.
        /// </summary>
        /// <param name="roofData">Raw roof geometry extracted from Revit.</param>
        /// <returns>Fully classified drainage graph with node types.</returns>
        public DrainGraphData Build(RoofGeometryData roofData)
        {
            // Create adjacency graph from downhill connections
            var adjacency = new Dictionary<XYZKey, List<XYZKey>>();

            foreach (var pair in roofData.DownhillGraph)
            {
                adjacency[pair.Key] = new List<XYZKey>(pair.Value);
            }

            // Classify nodes based on connection patterns
            var corners = new HashSet<XYZKey>();
            var ridges = new HashSet<XYZKey>();
            var drains = new HashSet<XYZKey>();
            var otherBoundaries = new HashSet<XYZKey>();

            // TODO: Implement actual classification logic
            // Example logic:
            // - Corners: Nodes with exactly 2 connections at right angles
            // - Ridges: Nodes with multiple connections forming a ridge line
            // - Drains: Lowest points with water collection
            // - Other boundaries: Remaining boundary nodes

            // For now, classify based on connection count (simplified)
            foreach (var node in roofData.BoundaryNodes)
            {
                if (adjacency.TryGetValue(node, out var connections))
                {
                    int connectionCount = connections?.Count ?? 0;

                    if (connectionCount == 0)
                    {
                        drains.Add(node); // Isolated node - potential drain
                    }
                    else if (connectionCount == 1)
                    {
                        corners.Add(node); // End point - corner
                    }
                    else if (connectionCount == 2)
                    {
                        ridges.Add(node); // Midpoint on ridge/valley
                    }
                    else
                    {
                        otherBoundaries.Add(node); // Complex intersection
                    }
                }
                else
                {
                    otherBoundaries.Add(node); // No connections
                }
            }

            return new DrainGraphData
            {
                Graph = adjacency,
                CornerNodes = corners,
                RidgeNodes = ridges,
                DrainNodes = drains,
                OtherBoundaryNodes = otherBoundaries
            };
        }

        /// <summary>
        /// Analyzes connection angles to determine if nodes form a corner.
        /// </summary>
        private bool IsCorner(XYZKey node, List<XYZKey> connections)
        {
            if (connections == null || connections.Count != 2)
                return false;

            // Check if the two connections form approximately a right angle
            var v1 = connections[0].ToXYZ() - node.ToXYZ();
            var v2 = connections[1].ToXYZ() - node.ToXYZ();

            double angle = v1.AngleTo(v2);
            double rightAngle = Math.PI / 2; // 90 degrees in radians

            // Allow some tolerance
            return Math.Abs(angle - rightAngle) < 0.17; // ~10 degrees tolerance
        }

        /// <summary>
        /// Determines if a node is a drain (lowest point in its local area).
        /// </summary>
        private bool IsDrain(XYZKey node, Dictionary<XYZKey, List<XYZKey>> graph)
        {
            if (!graph.TryGetValue(node, out var connections) || connections == null)
                return true; // Isolated node is a drain

            // Check if all connections are uphill from this node
            // (This assumes DownhillGraph direction is FROM higher TO lower)
            foreach (var neighbor in connections)
            {
                // If this node has any connections where it's the downhill end,
                // it's not the lowest point
                if (graph.TryGetValue(neighbor, out var neighborConnections))
                {
                    if (neighborConnections.Contains(node))
                    {
                        // Bidirectional connection - not a pure drain
                        return false;
                    }
                }
            }

            return true;
        }
    }
}