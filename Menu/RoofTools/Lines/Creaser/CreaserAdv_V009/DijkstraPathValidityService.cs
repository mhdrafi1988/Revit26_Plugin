// ==================================
// File: DijkstraPathValidityService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V008_00
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv.V009.Services
{
    /// <summary>
    /// Immutable result of a <see cref="DijkstraPathValidityService.FilterByPathValidity"/> run.
    /// </summary>
    public sealed class DijkstraFilterResult
    {
        public IList<Curve> Creases           { get; }
        public IList<Curve> Boundary          { get; }
        public int          RidgePoints       { get; }
        public int          DisconnectedPoints{ get; }

        public DijkstraFilterResult(IList<Curve> creases, IList<Curve> boundary, int ridgePoints, int disconnectedPoints)
        {
            Creases            = creases;
            Boundary           = boundary;
            RidgePoints        = ridgePoints;
            DisconnectedPoints = disconnectedPoints;
        }

        public static DijkstraFilterResult Empty =>
            new DijkstraFilterResult(new List<Curve>(), new List<Curve>(), 0, 0);
    }

    /// <summary>
    /// Validates each crease/boundary 3D curve against drain-path rules:
    ///
    ///   1. Directionality — endpoint(0) must be the higher-Z end (already
    ///      guaranteed by RoofSharedTopFaceCreaseService.NormalizeOrientation,
    ///      re-checked here defensively).
    ///
    ///   2. Minimum slope (optional, toggle-controlled) — an edge whose
    ///      slope% = (ΔZ / horizontal 2D length) × 100 falls below the
    ///      user-supplied MinimumSlopePercent is rejected before it ever
    ///      enters the graph. Near-vertical edges (horizontal length ≈ 0)
    ///      are never rejected on this basis.
    ///
    ///   3. Single path per node — each non-drain node keeps exactly ONE
    ///      outgoing descending edge: whichever neighbor has the lowest
    ///      dist-to-drain.
    ///
    ///   4. Ridge points — a node with no descending edge to any neighbor
    ///      is flagged and excluded entirely; none of its edges are kept.
    ///
    ///   5. Disconnected points — a node unreachable from any drain
    ///      (dist == Infinity) is flagged and excluded entirely.
    ///
    /// Graph construction: nodes are curve endpoints, merged within
    /// <see cref="NodeMergeToleranceMm"/> of each other. Edges are the curves
    /// themselves — no candidate-edge search, no face/arc validation, since
    /// the crease/boundary lines are already the real geometry.
    ///
    /// Algorithm reused from AutoSlopeByPoint's DijkstraPathEngine
    /// (multi-source reverse Dijkstra), adapted to this simpler line-segment
    /// graph and to a single-shortest-descent-per-node selection rule.
    ///
    /// NOTE (perf): node merging uses a linear scan per point (GetOrAddNode),
    /// which is O(n²) in point count. Fine for typical roof crease/boundary
    /// counts; if this ever becomes a bottleneck on very large roofs, switch
    /// to a spatial hash bucketed by rounded coordinates.
    /// </summary>
    public class DijkstraPathValidityService
    {
        private readonly LoggingService _log;

        // NOTE: not exposed in the UI (single ticked-by-default toggle only).
        // Flagging these assumed values — adjust if different tolerances are wanted.
        private const double NodeMergeToleranceMm = 8.0;   // "5-10mm" range requested — picked midpoint
        private const double DrainZToleranceMm    = 5.0;   // nodes within this of the minimum Z count as drains
        private const double DescentToleranceMm   = 1.0;   // edge must close this much distance-to-drain to count as "descending"
        private const double MmToFt               = 1.0 / 304.8;

        // (u = higher-Z endpoint node, v = lower-Z endpoint node, weight = 3D length, source list + index)
        private readonly struct Edge
        {
            public readonly int u, v, idx;
            public readonly double w;
            public readonly bool isBoundary;
            public Edge(int u, int v, double w, bool isBoundary, int idx)
            { this.u = u; this.v = v; this.w = w; this.isBoundary = isBoundary; this.idx = idx; }
        }

        public DijkstraPathValidityService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Filters crease and boundary curves together (single combined graph).
        /// Each non-drain node keeps at most one outgoing edge — its single
        /// shortest descending route to a drain. Ridge points (no valid
        /// descent) and disconnected points (unreachable from any drain) are
        /// excluded and their counts returned for the run summary.
        /// </summary>
        /// <param name="minimumSlopePercent">
        /// Minimum required slope, as a percentage (ΔZ / horizontal 2D length × 100).
        /// Ignored when <paramref name="enforceMinimumSlope"/> is false.
        /// </param>
        /// <param name="enforceMinimumSlope">Whether the slope check is active.</param>
        public DijkstraFilterResult FilterByPathValidity(
            IList<Curve> creaseCurves,
            IList<Curve> boundaryCurves,
            double minimumSlopePercent,
            bool enforceMinimumSlope)
        {
            creaseCurves   ??= new List<Curve>();
            boundaryCurves ??= new List<Curve>();

            if (creaseCurves.Count == 0 && boundaryCurves.Count == 0)
            {
                _log.Info("No curves to validate against drain paths.");
                return DijkstraFilterResult.Empty;
            }

            var nodes = new List<XYZ>();
            var edges = BuildEdges(creaseCurves, boundaryCurves, nodes, minimumSlopePercent, enforceMinimumSlope, out int slopeRejected);

            if (nodes.Count == 0)
            {
                _log.Info("Dijkstra path validity: no nodes remained after slope filtering.");
                return DijkstraFilterResult.Empty;
            }

            var adj = BuildAdjacency(edges);
            var drains = FindDrainNodes(nodes);

            _log.Info($"Dijkstra path validity: {nodes.Count} nodes, {edges.Count} edges, {drains.Count} drain node(s) at minimum elevation" +
                      (enforceMinimumSlope ? $", {slopeRejected} edge(s) rejected below {minimumSlopePercent:F2}% slope." : "."));

            var dist = RunReverseDijkstra(nodes.Count, drains, adj);

            var chosenHops = SelectShortestHopPerNode(nodes, adj, drains, dist, out int ridgeCount, out int disconnectedCount);

            var (keptCreases, keptBoundary, removed) = ReconstructKeptEdges(edges, chosenHops, creaseCurves, boundaryCurves);

            _log.Info($"Dijkstra path validity filter: kept {keptCreases.Count} crease + {keptBoundary.Count} boundary, " +
                      $"removed {removed}, ridge points {ridgeCount}, disconnected points {disconnectedCount}.");

            return new DijkstraFilterResult(keptCreases, keptBoundary, ridgeCount, disconnectedCount);
        }

        // --------------------------------------------------
        // Step 1 — collect edges (with node merging + slope rejection)
        // --------------------------------------------------
        private List<Edge> BuildEdges(
            IList<Curve> creaseCurves,
            IList<Curve> boundaryCurves,
            List<XYZ> nodes,
            double minimumSlopePercent,
            bool enforceMinimumSlope,
            out int slopeRejected)
        {
            double mergeTolFt = NodeMergeToleranceMm * MmToFt;
            int GetOrAddNode(XYZ p)
            {
                for (int i = 0; i < nodes.Count; i++)
                    if (nodes[i].DistanceTo(p) <= mergeTolFt)
                        return i;
                nodes.Add(p);
                return nodes.Count - 1;
            }

            var edges = new List<Edge>();
            int rejected = 0;

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

                    double dz = p0.Z - p1.Z;
                    double horizLenFt = new XYZ(p0.X - p1.X, p0.Y - p1.Y, 0).GetLength();

                    if (enforceMinimumSlope && horizLenFt > 1e-9)
                    {
                        double slopePct = (dz / horizLenFt) * 100.0;
                        if (slopePct < minimumSlopePercent)
                        {
                            _log.Warning($"{(isBoundary ? "Boundary" : "Crease")} curve #{k} rejected — slope {slopePct:F2}% below minimum {minimumSlopePercent:F2}%.");
                            rejected++;
                            continue;
                        }
                    }
                    // horizLenFt ≈ 0 (near-vertical edge): slope is effectively infinite — never rejected here.

                    int u = GetOrAddNode(p0);
                    int v = GetOrAddNode(p1);
                    if (u == v)
                    {
                        _log.Warning($"{(isBoundary ? "Boundary" : "Crease")} curve #{k} collapsed to a single node — excluded.");
                        continue;
                    }

                    edges.Add(new Edge(u, v, p0.DistanceTo(p1), isBoundary, k));
                }
            }

            CollectEdges(creaseCurves, false);
            CollectEdges(boundaryCurves, true);

            slopeRejected = rejected;
            return edges;
        }

        // --------------------------------------------------
        // Step 2 — adjacency list
        // --------------------------------------------------
        private static Dictionary<int, List<(int nb, double w)>> BuildAdjacency(List<Edge> edges)
        {
            var adj = new Dictionary<int, List<(int nb, double w)>>();
            foreach (var e in edges)
            {
                if (!adj.TryGetValue(e.u, out var lu)) adj[e.u] = lu = new List<(int, double)>();
                if (!adj.TryGetValue(e.v, out var lv)) adj[e.v] = lv = new List<(int, double)>();
                lu.Add((e.v, e.w));
                lv.Add((e.u, e.w));
            }
            return adj;
        }

        // --------------------------------------------------
        // Step 3 — drain detection (lowest-Z nodes, within tolerance)
        // --------------------------------------------------
        private static HashSet<int> FindDrainNodes(List<XYZ> nodes)
        {
            double zTolFt = DrainZToleranceMm * MmToFt;
            double minZ = nodes.Min(p => p.Z);
            var drains = new HashSet<int>();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].Z <= minZ + zTolFt)
                    drains.Add(i);
            return drains;
        }

        // --------------------------------------------------
        // Step 4 — multi-source reverse Dijkstra
        // --------------------------------------------------
        private static double[] RunReverseDijkstra(int nodeCount, HashSet<int> drains, Dictionary<int, List<(int nb, double w)>> adj)
        {
            var dist = new double[nodeCount];
            for (int i = 0; i < nodeCount; i++) dist[i] = double.PositiveInfinity;

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

            return dist;
        }

        // --------------------------------------------------
        // Step 5 — pick each non-drain node's single shortest descending hop;
        // flag ridge points (no valid descent) and disconnected points
        // (unreachable from any drain).
        // --------------------------------------------------
        private HashSet<(int, int)> SelectShortestHopPerNode(
            List<XYZ> nodes,
            Dictionary<int, List<(int nb, double w)>> adj,
            HashSet<int> drains,
            double[] dist,
            out int ridgeCount,
            out int disconnectedCount)
        {
            string PointStr(int i) => $"({nodes[i].X:F2}, {nodes[i].Y:F2}, {nodes[i].Z:F2})";

            double descentTolFt = DescentToleranceMm * MmToFt;
            var chosenHops = new HashSet<(int, int)>(); // undirected (u,v) pairs that survive

            int ridges = 0;
            int disconnected = 0;

            for (int u = 0; u < nodes.Count; u++)
            {
                if (drains.Contains(u)) continue; // drains have no outgoing edge to select

                if (double.IsPositiveInfinity(dist[u]))
                {
                    disconnected++;
                    _log.Warning($"Disconnected point at node {u} {PointStr(u)} — unreachable from any drain, excluded.");
                    continue;
                }

                if (!adj.TryGetValue(u, out var nbrs))
                {
                    ridges++;
                    _log.Warning($"Ridge point at node {u} {PointStr(u)} — no neighbors, excluded.");
                    continue;
                }

                int best = -1;
                double bestDist = dist[u] - descentTolFt; // must strictly improve beyond tolerance
                foreach (var (nb, w) in nbrs)
                {
                    if (dist[nb] < bestDist)
                    {
                        bestDist = dist[nb];
                        best = nb;
                    }
                }

                if (best == -1)
                {
                    ridges++;
                    _log.Warning($"Ridge point at node {u} {PointStr(u)} — no valid descending edge, excluded.");
                    continue;
                }

                chosenHops.Add((Math.Min(u, best), Math.Max(u, best)));
            }

            ridgeCount = ridges;
            disconnectedCount = disconnected;
            return chosenHops;
        }

        // --------------------------------------------------
        // Step 6 — keep only edges matching a chosen hop
        // --------------------------------------------------
        private static (List<Curve> creases, List<Curve> boundary, int removed) ReconstructKeptEdges(
            List<Edge> edges,
            HashSet<(int, int)> chosenHops,
            IList<Curve> creaseCurves,
            IList<Curve> boundaryCurves)
        {
            var keptCreases  = new List<Curve>();
            var keptBoundary = new List<Curve>();
            int removed = 0;

            foreach (var e in edges)
            {
                bool matchesHop = chosenHops.Contains((Math.Min(e.u, e.v), Math.Max(e.u, e.v)));

                if (matchesHop)
                {
                    if (e.isBoundary) keptBoundary.Add(boundaryCurves[e.idx]);
                    else              keptCreases.Add(creaseCurves[e.idx]);
                }
                else
                {
                    removed++;
                }
            }

            return (keptCreases, keptBoundary, removed);
        }
    }
}
