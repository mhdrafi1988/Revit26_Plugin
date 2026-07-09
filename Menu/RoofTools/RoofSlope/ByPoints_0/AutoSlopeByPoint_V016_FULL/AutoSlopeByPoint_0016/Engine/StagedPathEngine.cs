// =======================================================
// File: StagedPathEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V016
// Purpose: Orchestrates the 6-step staged path pipeline on top of
//          DijkstraPathEngine:
//   1. Direct path check   — straight, unobstructed line to a drain.
//   2. Graph path check    — plain straight-line visibility graph
//                            (DijkstraPathEngine, enableArcTangents=false).
//   3-5. Arc-tangent path  — full visibility graph with convex-obstacle
//                            tangents/bitangents (enableArcTangents=true),
//                            using the run-wide MultiArcMode for 3+ arcs.
//   6. Final calculation   — per point: Direct wins outright if valid
//                            (nothing beats a straight line); otherwise
//                            compare Graph vs ArcTangent and take the
//                            shorter of the two that succeeded.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.V016.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V016.Core.Engine
{
    public enum PathMethod { Direct, Graph, ArcTangent, Unreachable }

    public struct StagedPathResult
    {
        public double DistanceFt;
        public PathMethod Method;
        public string ArcTypeSummary;
        public List<DijkstraPathEngine.PathHop> Hops;
    }

    public class StagedPathEngine
    {
        private readonly List<SlabShapeVertex> _verts;
        private readonly List<XYZ> _drainPoints;
        private readonly HashSet<int> _drainIndices;

        private readonly DijkstraPathEngine _graphOnlyEngine;   // step 2
        private readonly DijkstraPathEngine _arcTangentEngine;  // steps 3-5

        private double[] _graphDist;
        private double[] _arcDist;
        private readonly MultiArcMode _multiArcMode;

        public StagedPathEngine(
            List<SlabShapeVertex> vertices,
            Face topFace,
            double edgeThresholdFt,
            List<Arc> arcs,
            double curveTolFt,
            HashSet<int> drainIndices,
            List<XYZ> drainPoints,
            MultiArcMode multiArcMode)
        {
            _verts = vertices;
            _drainIndices = drainIndices;
            _drainPoints = drainPoints;
            _multiArcMode = multiArcMode;

            _graphOnlyEngine = new DijkstraPathEngine(
                vertices, topFace, edgeThresholdFt, arcs, curveTolFt,
                enableArcTangents: false);

            _arcTangentEngine = new DijkstraPathEngine(
                vertices, topFace, edgeThresholdFt, arcs, curveTolFt,
                enableArcTangents: true, multiArcMode: multiArcMode);
        }

        /// <summary>Runs steps 2 and 3-5 once (multi-source Dijkstra) up front for all vertices.</summary>
        public void ComputeAll()
        {
            _graphDist = _graphOnlyEngine.ComputeAllDistances(_drainIndices);
            _arcDist = _arcTangentEngine.ComputeAllDistances(_drainIndices);
        }

        /// <summary>Full staged result for one vertex (steps 1 + 6 comparison against the precomputed step 2/3-5 distances).</summary>
        public StagedPathResult Resolve(int vertexIndex)
        {
            if (_graphDist == null || _arcDist == null)
                throw new InvalidOperationException("Call ComputeAll() before Resolve().");

            XYZ from = _verts[vertexIndex].Position;

            // ── Step 1: direct check ────────────────────────────────────
            double bestDirect = double.PositiveInfinity;
            foreach (XYZ drain in _drainPoints)
            {
                if (drain == null) continue;
                if (!_graphOnlyEngine.IsDirectlyVisible(from, drain)) continue;
                double d = from.DistanceTo(drain);
                if (d < bestDirect) bestDirect = d;
            }

            if (!double.IsInfinity(bestDirect))
            {
                return new StagedPathResult
                {
                    DistanceFt = bestDirect,
                    Method = PathMethod.Direct,
                    ArcTypeSummary = "",
                    Hops = new List<DijkstraPathEngine.PathHop>
                    {
                        new DijkstraPathEngine.PathHop($"line v{vertexIndex} → drain", bestDirect, false)
                    }
                };
            }

            // ── Step 6: compare graph (step 2) vs arc-tangent (steps 3-5) ──
            double graphD = _graphDist[vertexIndex];
            double arcD = _arcDist[vertexIndex];

            bool graphOk = !double.IsInfinity(graphD);
            bool arcOk = !double.IsInfinity(arcD);

            if (!graphOk && !arcOk)
            {
                return new StagedPathResult
                {
                    DistanceFt = double.PositiveInfinity,
                    Method = PathMethod.Unreachable,
                    ArcTypeSummary = "",
                    Hops = new List<DijkstraPathEngine.PathHop>()
                };
            }

            bool useArc = arcOk && (!graphOk || arcD <= graphD);

            if (useArc)
            {
                var hops = _arcTangentEngine.GetPathFrom(vertexIndex);
                string arcSummary = SummarizeArcs(hops);
                return new StagedPathResult
                {
                    DistanceFt = arcD,
                    Method = PathMethod.ArcTangent,
                    ArcTypeSummary = arcSummary,
                    Hops = hops
                };
            }

            return new StagedPathResult
            {
                DistanceFt = graphD,
                Method = PathMethod.Graph,
                ArcTypeSummary = "",
                Hops = _graphOnlyEngine.GetPathFrom(vertexIndex)
            };
        }

        public string GetSkipReason(int vertexIndex) => _arcTangentEngine.GetSkipReason(vertexIndex);

        private string SummarizeArcs(List<DijkstraPathEngine.PathHop> hops)
        {
            int arcHops = hops.Count(h => h.IsArcHop);
            if (arcHops == 0) return "Convex (tangent only)";
            if (arcHops == 1) return "Convex (single arc)";
            return $"Convex x{arcHops} ({_multiArcMode})";
        }
    }
}
