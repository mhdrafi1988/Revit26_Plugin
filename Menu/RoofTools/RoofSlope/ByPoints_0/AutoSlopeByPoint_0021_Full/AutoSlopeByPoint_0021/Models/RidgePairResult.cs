// =======================================================
// File: RidgePairResult.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V021
// New in V021 — Ridge Point Detection (opt-in).
// UPDATED: corridor-based multi-point search (was: single closest
// vertex per perpendicular direction, max 2 per pair). Now every
// unclaimed roof vertex within CorridorWidthMm of the (infinite)
// perpendicular line counts, regardless of side/direction — so a
// pair can resolve 0, 1, or many ridge points.
//
// One instance per adjacent drain-group pair (Delaunay-derived).
// Carries enough detail for the processing log, the "Ridge Points"
// UI metric cards, and the Excel detailed export's new Ridge sheet.
// =======================================================

using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Models
{
    public class RidgePairResult
    {
        /// <summary>1-based display index of this pair, in the order it was processed (shortest group-center distance first).</summary>
        public int PairIndex { get; set; }

        /// <summary>Index of the first drain group in the pair (into the engine's internal group list).</summary>
        public int GroupAIndex { get; set; }

        /// <summary>Index of the second drain group in the pair.</summary>
        public int GroupBIndex { get; set; }

        /// <summary>Center (centroid) of group A.</summary>
        public XYZ GroupACenter { get; set; }

        /// <summary>Center (centroid) of group B.</summary>
        public XYZ GroupBCenter { get; set; }

        /// <summary>Straight-line distance between group centers, internal feet.</summary>
        public double CenterDistanceFt { get; set; }

        /// <summary>Corridor/edge-proximity tolerance actually used for this pair's search, internal feet (for traceability in exports/logs).</summary>
        public double CorridorWidthFt { get; set; }

        /// <summary>Start point of the actual ridge line searched (Voronoi edge segment, or the midpoint-perpendicular fallback line).</summary>
        public XYZ RidgeLineStart { get; set; }

        /// <summary>End point of the actual ridge line searched.</summary>
        public XYZ RidgeLineEnd { get; set; }

        /// <summary>
        /// True if this pair's ridge line came from a real Voronoi edge (the
        /// mathematically correct boundary between the two groups' territories).
        /// False means the degenerate-case fallback was used instead (fewer than
        /// 3 groups total, or collinear group centers) — the old midpoint-
        /// perpendicular-line method, extended across the roof's bounding box.
        /// </summary>
        public bool UsedVoronoiEdge { get; set; }

        /// <summary>
        /// Every roof vertex index (into the roof's SlabShapeVertex list) that fell
        /// within the corridor and was not already claimed by an earlier-processed
        /// pair. Replaces the old fixed PositiveSideVertexIndex/NegativeSideVertexIndex
        /// pair — can now hold any number of entries, including zero.
        /// </summary>
        public List<int> MatchedVertexIndices { get; set; } = new List<int>();

        /// <summary>
        /// True if this pair was skipped entirely (no ridge vertex found within
        /// the corridor). Logged as Info, per spec — these vertices simply fall
        /// back to standard per-vertex Dijkstra.
        /// </summary>
        public bool Skipped => MatchedVertexIndices.Count == 0;

        /// <summary>Count of ridge points actually resolved for this pair (0 or more — no cap).</summary>
        public int ResolvedCount => MatchedVertexIndices.Count;
    }
}
