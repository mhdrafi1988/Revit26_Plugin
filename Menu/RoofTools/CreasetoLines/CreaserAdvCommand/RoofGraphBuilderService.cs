using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_adv_V001.Models;

namespace Revit26_Plugin.Creaser_adv_V001.Services
{
    /// <summary>
    /// Builds a roof topology graph.
    /// UPDATED: filters out nodes and edges inside roof voids/openings.
    /// </summary>
    public class RoofGraphBuilderService
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public RoofGraph Build(FootPrintRoof roof)
        {
            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            Document doc = roof.Document;

            // 1️⃣ Extract void loops (2D)
            IList<CurveLoop> voidLoops = GetRoofVoidLoops(roof);

            // 2️⃣ Build initial graph (your existing logic)
            RoofGraph graph = BuildBaseGraph(roof);

            // 3️⃣ Filter nodes inside voids
            var validNodes =
                graph.Nodes
                    .Where(n => !IsInsideAnyVoid(n.Point, voidLoops))
                    .ToList();

            // 4️⃣ Rebuild neighbor connections excluding void crossings
            foreach (GraphNode node in validNodes)
            {
                // Clear the existing neighbors
                var neighbors = node.Neighbors.ToList();
                foreach (var n in neighbors)
                {
                    if (!validNodes.Contains(n) || EdgeCrossesAnyVoid(node.Point, n.Point, voidLoops))
                    {
                        node.Neighbors.Remove(n);
                    }
                }
            }

            // 5️⃣ Reassign filtered collections
            graph.Nodes.Clear();
            foreach (var node in validNodes)
            {
                graph.Nodes.Add(node);
            }

            // Instead of assigning to the property, clear and add to the collection
            graph.CornerNodes.Clear();
            foreach (var node in graph.CornerNodes.Where(validNodes.Contains).ToList())
            {
                graph.CornerNodes.Add(node);
            }

            graph.DrainNodes.Clear();
            foreach (var node in graph.DrainNodes.Where(validNodes.Contains).ToList())
            {
                graph.DrainNodes.Add(node);
            }

            return graph;
        }

        // ============================================================
        // 🔹 VOID EXTRACTION
        // ============================================================

        private IList<CurveLoop> GetRoofVoidLoops(FootPrintRoof roof)
        {
            var loops = new List<CurveLoop>();

            // Use FindInserts on the FootPrintRoof (HostObject), not on Element
            foreach (Opening opening in roof
                         .FindInserts(true, false, false, false)
                         .Select(id => roof.Document.GetElement(id))
                         .OfType<Opening>())
            {
                if (opening.BoundaryCurves != null)
                {
                    foreach (CurveArray ca in opening.BoundaryCurves)
                    {
                        var loop = new CurveLoop();
                        foreach (Curve c in ca)
                            loop.Append(c);

                        loops.Add(loop);
                    }
                }
            }

            return loops;
        }

        // ============================================================
        // 🔹 VOID TESTS (2D)
        // ============================================================

        private bool IsInsideAnyVoid(
            XYZ point,
            IList<CurveLoop> voidLoops)
        {
            foreach (CurveLoop loop in voidLoops)
            {
                if (IsPointInsideLoop2D(point, loop))
                    return true;
            }
            return false;
        }

        private bool EdgeCrossesAnyVoid(
            XYZ p1,
            XYZ p2,
            IList<CurveLoop> voidLoops)
        {
            Line edge = Line.CreateBound(p1, p2);

            foreach (CurveLoop loop in voidLoops)
            {
                foreach (Curve c in loop)
                {
                    if (edge.Intersect(c) != SetComparisonResult.Disjoint)
                        return true;
                }
            }
            return false;
        }

        // ============================================================
        // 🔹 POINT-IN-POLYGON (2D RAY CAST)
        // ============================================================

        private bool IsPointInsideLoop2D(XYZ point, CurveLoop loop)
        {
            var pts =
                loop
                    .Select(c => c.GetEndPoint(0))
                    .ToList();

            bool inside = false;
            int j = pts.Count - 1;

            for (int i = 0; i < pts.Count; i++)
            {
                XYZ pi = pts[i];
                XYZ pj = pts[j];

                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X <
                     (pj.X - pi.X) *
                     (point.Y - pi.Y) /
                     (pj.Y - pi.Y + 1e-9) +
                     pi.X))
                {
                    inside = !inside;
                }

                j = i;
            }

            return inside;
        }

        // ============================================================
        // 🔹 EXISTING GRAPH BUILD (UNCHANGED)
        // ============================================================

        private RoofGraph BuildBaseGraph(FootPrintRoof roof)
        {
            // 🔴 KEEP YOUR EXISTING IMPLEMENTATION HERE
            // This method should populate:
            // - graph.Nodes
            // - graph.CornerNodes
            // - graph.DrainNodes
            // - node.Neighbors

            throw new NotImplementedException(
                "Use your existing graph construction logic here.");
        }
    }
}
