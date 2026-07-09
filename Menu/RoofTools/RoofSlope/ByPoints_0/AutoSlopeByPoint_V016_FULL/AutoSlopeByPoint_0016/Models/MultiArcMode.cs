// =======================================================
// File: MultiArcMode.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V016
// Purpose: Global run-wide choice for how a path that crosses
//          3+ arcs is built. One toggle, applies to every vertex
//          in the run — per-vertex result (which method/arc-count
//          actually won) is still reported individually.
// =======================================================

namespace Revit26_Plugin.AutoSlopeByPoint.V016.Core.Models
{
    public enum MultiArcMode
    {
        /// <summary>
        /// Arc-by-arc: bitangents are only computed between arcs adjacent
        /// in a nearest-neighbor chain. Cheaper, mirrors "hug one arc, then
        /// the next" walking behavior.
        /// </summary>
        Sequential,

        /// <summary>
        /// All-pairs: bitangents are computed between every pair of arcs
        /// and Dijkstra is left to pick whichever combination is shortest.
        /// More expensive for many arcs, can find shortcuts Sequential misses.
        /// </summary>
        PairwiseCombination
    }
}
