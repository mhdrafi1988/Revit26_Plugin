using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_adv_V001.Models;
using Revit26_Plugin.Creaser_adv_V001.Helpers;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// FINAL graph builder.
    /// Fully connected graph with slope-aware pathfinding.
    /// </summary>
    public class RoofGraphBuilderService
    {
        private const double ZTolerance = 0.05; // feet

        public RoofGraph Build(RoofBase roof)
        {
            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null)
                throw new InvalidOperationException("Roof does not support shape editing.");

            IList<SlabShapeVertex> vertices = editor.SlabShapeVertices?.Cast<SlabShapeVertex>().ToList();
            if (vertices == null || vertices.Count == 0)
                throw new InvalidOperationException("No shape vertices found.");

            RoofGraph graph = new RoofGraph();

            int id = 0;

            // ------------------------------------------------
            // 1. Create nodes
            // ------------------------------------------------
            foreach (SlabShapeVertex v in vertices)
            {
                graph.Nodes.Add(new GraphNode(id++, v.Position));
            }

            // ------------------------------------------------
            // 2. FULL CONNECTIVITY (CRITICAL)
            // ------------------------------------------------
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                for (int j = 0; j < graph.Nodes.Count; j++)
                {
                    if (i == j) continue;
                    Connect(graph.Nodes[i], graph.Nodes[j]);
                }
            }

            // ------------------------------------------------
            // 3. Corners = highest Z
            // ------------------------------------------------
            double maxZ = graph.Nodes.Max(n => n.Z);

            foreach (GraphNode node in graph.Nodes)
            {
                if (Math.Abs(node.Z - maxZ) <= ZTolerance)
                {
                    node.IsCorner = true;
                    graph.CornerNodes.Add(node);
                }
            }

            // ------------------------------------------------
            // 4. Drains = lowest Z (excluding corners)
            // ------------------------------------------------
            double minZ = graph.Nodes.Min(n => n.Z);

            foreach (GraphNode node in graph.Nodes)
            {
                if (node.IsCorner) continue;

                if (Math.Abs(node.Z - minZ) <= ZTolerance)
                {
                    node.IsDrain = true;
                    graph.DrainNodes.Add(node);
                }
            }

            return graph;
        }

        private static void Connect(GraphNode from, GraphNode to)
        {
            if (!from.Neighbors.Contains(to))
                from.Neighbors.Add(to);
        }
    }
}
