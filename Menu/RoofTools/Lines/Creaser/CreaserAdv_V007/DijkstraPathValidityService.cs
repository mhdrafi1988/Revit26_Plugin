// ==================================
// File: DijkstraPathValidityService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V007_00
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V007_00.Services
{
    /// <summary>
    /// Validates each crease/boundary 3D curve against two conditions:
    ///
    ///   1. Directionality — endpoint(0) must be the higher-Z end (already
    ///      guaranteed by RoofSharedTopFaceCreaseService.NormalizeOrientation,
    ///      re-checked here defensively).
    ///
    ///   2. Path integrity — the segment must genuinely descend toward SOME
    ///      drain: dist[lowerEnd] must be strictly less than dist[higherEnd]
    ///      (beyond DescentToleranceMm, to ignore floating-point noise on
    ///      near-flat edges). This keeps every valid downhill direction from a
    ///      ridge point — not just the single globally-shortest one — so a
    ///      ridge that sheds to two or more different drains keeps all of
    ///      those lines, not only whichever happened to be marginally shorter.
    ///
    /// Graph construction: nodes are curve endpoints, merged within
    /// <see cref="NodeMergeToleranceMm"/> of each other. Edges are the curves
    /// themselves — no candidate-edge search, no face/arc validation, since
    /// the crease/boundary lines are already the real geometry.
    ///
    /// Algorithm reused from AutoSlopeByPoint's DijkstraPathEngine
    /// (multi-source reverse Dijkstra), adapted to this simpler line-segment
    /// graph and to a "keep all valid descents" rule instead of a single
    /// shortest-path-tree hop per node.
    /// </summary>
    public class DijkstraPathValidityService
    {
        private readonly LoggingService _log;

        // NOTE: not exposed in the UI (single ticked-by-default toggle only).
        // Flagging these assumed values — adjust if different tolerances are wanted.
        private const double NodeMergeToleranceMm = 8.0;   // "5-10mm" range requested — picked midpoint
        private const double DrainZToleranceMm    = 5.0;   // nodes within this of the minimum Z count as drains
        private const double DescentToleranceMm   = 1.0;   // edge must close this much distance-to-drain to count as "descending"

        public DijkstraPathValidityService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Filters crease and boundary curves together (single combined graph),
        /// returning only the curves that genuinely descend toward some drain.
        /// A node with multiple downhill edges toward different drains keeps
        /// all of them, not just the single shortest one.
        /// </summary>
        public (IList<Curve> Creases, IList<Curve> Boundary) FilterByPathValidity(
            IList<Curve> creaseCurves,
            IList<Curve> boundaryCurves)
        {
            creaseCurves   ??= new List<Curve>();
            boundaryCurves ??= new List<Curve>();

            if (creaseCurves.Count == 0 && boundaryCurves.Count == 0)
            {
                _log.Info("No curves to validate against drain paths.");
                return (new List<Curve>(), new List<Curve>());
            }

            double mergeTolFt = NodeMergeToleranceMm / 304.8;
            double zTolFt     = DrainZToleranceMm    / 304.8;

            var nodes = new List<XYZ>();

            int GetOrAddNode(XYZ p)
            {
                for (int i = 0; i < nodes.Count; i++)
                    if (nodes[i].DistanceTo(p) <= mergeTolFt)
                        return i;
                nodes.Add(p);
                return nodes.Count - 1;
            }

            // (u = higher-Z endpoint node, v = lower-Z endpoint node, weight, source list + index)
            var edges = new List<(int u, int v, double w, bool isBoundary, int idx)>();

            void CollectEdges(IList<Curve> curves, bool isBoundary)
            {
                for (int k = 0; k < curves.Count; k++)
                {
                    Curve c = curves[k];
                    if (c == null) continue;

                    XYZ p0 = c.GetEndPoint(0);
                    XYZ p1 = c.GetEndPoint(1);

                    // Defensive re-check: endpoint(0) should already be the higher-Z
                    // end (NormalizeOrientation guarantees this upstream).
                    if (p0.Z < p1.Z) (p0, p1) = (p1, p0);

                    int u = GetOrAddNode(p0);
                    int v = GetOrAddNode(p1);
                    if (u == v)
                    {
                        _log.Warning($"{(isBoundary ? "Boundary" : "Crease")} curve #{k} collapsed to a single node — excluded.");
                        continue;
                    }

                    edges.Add((u, v, p0.DistanceTo(p1), isBoundary, k));
                }
            }

            CollectEdges(creaseCurves, false);
            CollectEdges(boundaryCurves, true);

            // Build adjacency
            var adj = new Dictionary<int, List<(int nb, double w)>>();
            foreach (var e in edges)
            {
                if (!adj.TryGetValue(e.u, out var lu)) adj[e.u] = lu = new List<(int, double)>();
                if (!adj.TryGetValue(e.v, out var lv)) adj[e.v] = lv = new List<(int, double)>();
                lu.Add((e.v, e.w));
                lv.Add((e.u, e.w));
            }

            // Drain nodes = all nodes within DrainZToleranceMm of the graph's minimum Z
            double minZ = nodes.Min(p => p.Z);
            var drains = new HashSet<int>();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].Z <= minZ + zTolFt)
                    drains.Add(i);

            _log.Info($"Dijkstra path validity: {nodes.Count} nodes, {edges.Count} edges, {drains.Count} drain node(s) at minimum elevation.");

            // Multi-source reverse Dijkstra — dist[i] = shortest 3D distance from
            // node i to its nearest drain. (No predecessor tracking needed anymore:
            // we keep every edge that descends, not just the single shortest hop.)
            int n = nodes.Count;
            var dist = new double[n];
            for (int i = 0; i < n; i++) dist[i] = double.PositiveInfinity;

            var pq = new SortedSet<(double, int)>(
                Comparer<(double, int)>.Create((a, b) =>
                {
                    int cmp = a.Item1.CompareTo(b.Item1);
                    return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
                }));

            foreach (int d in drains)
            {
                dist[d] = 0;
                pq.Add((0, d));
            }

            while (pq.Count > 0)
            {
                var (dd, vv) = pq.Min;
                pq.Remove(pq.Min);
                if (dd > dist[vv]) continue; // stale entry

                if (!adj.TryGetValue(vv, out var nbrs)) continue;

                foreach (var (nb, w) in nbrs)
                {
                    double nd = dd + w;
                    if (nd < dist[nb])
                    {
                        dist[nb] = nd;
                        pq.Add((nd, nb));
                    }
                }
            }

            // Keep edge (u = higher-Z, v = lower-Z) whenever it genuinely descends
            // toward SOME drain — dist[v] must be closer to a drain than dist[u] by
            // more than DescentToleranceMm. A ridge point with several downhill
            // edges toward different drains keeps all of them; only edges that go
            // away from every drain (or are unreachable from any drain) are dropped.
            double descentTolFt = DescentToleranceMm / 304.8;
            var keptCreases  = new List<Curve>();
            var keptBoundary = new List<Curve>();
            int removed = 0;

            foreach (var e in edges)
            {
                bool reachable = !double.IsPositiveInfinity(dist[e.u]) && !double.IsPositiveInfinity(dist[e.v]);
                bool descends  = reachable && dist[e.v] < dist[e.u] - descentTolFt;

                if (descends)
                {
                    if (e.isBoundary) keptBoundary.Add(boundaryCurves[e.idx]);
                    else              keptCreases.Add(creaseCurves[e.idx]);
                }
                else
                {
                    removed++;
                }
            }

            _log.Info($"Dijkstra path validity filter: kept {keptCreases.Count} crease + {keptBoundary.Count} boundary, removed {removed}");
            return (keptCreases, keptBoundary);
        }
    }
}
