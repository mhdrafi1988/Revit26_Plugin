// =======================================================
// File: RidgeJunctionResult.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V021
// New in V021 (3rd pass) — multi-group (3+) ridge point support at
// Voronoi VERTICES (not edges). A Voronoi vertex is the point
// equidistant from 3+ group centers — geometrically the circumcenter
// of the Delaunay triangle formed by those groups. Every roof vertex
// within tolerance of that circumcenter becomes a ridge point whose
// elevation reference is the FARTHEST of the 3+ groups (same "farther
// wins" rule as pairwise edges, extended to N groups).
//
// Junctions are processed BEFORE pairwise edges (confirmed: junctions
// are more specific/significant, get first claim on a vertex), ordered
// smallest-circumradius-first among themselves (confirmed default).
// =======================================================

using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Models
{
    public class RidgeJunctionResult
    {
        /// <summary>1-based display index, in the order this junction was processed (smallest circumradius first).</summary>
        public int JunctionIndex { get; set; }

        /// <summary>The 3 (or more, if ever extended) drain group indices meeting at this junction.</summary>
        public List<int> GroupIndices { get; set; } = new List<int>();

        /// <summary>The Voronoi vertex (Delaunay triangle circumcenter), projected to real 3D space.</summary>
        public XYZ JunctionPoint { get; set; }

        /// <summary>Circumradius of the underlying Delaunay triangle, internal feet — used for processing order and traceability.</summary>
        public double CircumRadiusFt { get; set; }

        /// <summary>Tolerance actually used for this junction's vertex search, internal feet.</summary>
        public double ToleranceFt { get; set; }

        /// <summary>Every unclaimed roof vertex index found within tolerance of the junction point.</summary>
        public List<int> MatchedVertexIndices { get; set; } = new List<int>();

        /// <summary>True if no roof vertex was found near this junction — falls back to standard/edge-based Dijkstra for those vertices.</summary>
        public bool Skipped => MatchedVertexIndices.Count == 0;

        /// <summary>Count of ridge points actually resolved at this junction.</summary>
        public int ResolvedCount => MatchedVertexIndices.Count;
    }
}
