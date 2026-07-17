// =======================================================
// File: VertexData.cs
// Changes:
//   Added ElevationFromModel_mm — the elevation re-read
//   from the roof vertex AFTER the slope transaction
//   commits. Compared against ElevationOffsetMm (which
//   is calculated from path × slope) to detect any silent
//   adjustments Revit made during commit.
// =======================================================

using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Models
{
    public class VertexData
    {
        public int VertexIndex { get; set; }
        public XYZ Position { get; set; }
        public double PathLengthMeters { get; set; }

        // Elevation calculated from: PathLength × SlopePercent
        // This is what the engine WROTE to the vertex.
        private double _elevationOffsetMm;
        public double ElevationOffsetMm
        {
            get => Math.Round(_elevationOffsetMm, 0);
            set => _elevationOffsetMm = value;
        }

        // Elevation READ BACK from the roof vertex after tx.Commit().
        // Reflects what Revit actually stored — may differ from
        // ElevationOffsetMm if Revit clamped or adjusted the value.
        private double _elevationFromModel_mm;
        public double ElevationFromModel_mm
        {
            get => Math.Round(_elevationFromModel_mm, 0);
            set => _elevationFromModel_mm = value;
        }

        // Difference: model value minus calculated value.
        // Zero = Revit accepted exactly what was written.
        // Non-zero = Revit silently adjusted the vertex.
        public double ElevationDiff_mm => ElevationFromModel_mm - ElevationOffsetMm;

        public int NearestDrainIndex { get; set; }
        public XYZ DirectionVector { get; set; }
        public bool WasProcessed { get; set; }

        public string Direction =>
            DirectionVector != null ?
            $"{DirectionVector.X:F3},{DirectionVector.Y:F3},{DirectionVector.Z:F3}" :
            "0,0,0";

        // ── Ridge Point Detection (V021, opt-in) ─────────────────────────
        /// <summary>True if this vertex was selected as a ridge point (between two adjacent drain groups) rather than routed by standard nearest-drain Dijkstra.</summary>
        public bool IsRidgePoint { get; set; }

        /// <summary>Index into the ridge-pair results list (RidgePairResult) this vertex belongs to, or -1 if not from a pairwise edge (either not a ridge point, or sourced from a junction instead — see IsJunctionPoint).</summary>
        public int RidgePairIndex { get; set; } = -1;

        /// <summary>
        /// True if this ridge point came from a multi-group (3+) Voronoi JUNCTION
        /// rather than a pairwise edge. When true, RidgeJunctionIndex (not
        /// RidgePairIndex) identifies which junction, and RidgeReferenceGroupIndex
        /// is the FARTHEST of the 3+ groups meeting there (same rule as pairwise,
        /// extended to N groups).
        /// </summary>
        public bool IsJunctionPoint { get; set; }

        /// <summary>Index into the junction results list (RidgeJunctionResult) this vertex belongs to, or -1 if not from a junction.</summary>
        public int RidgeJunctionIndex { get; set; } = -1;

        /// <summary>
        /// Group index whose nearest drain was used as the elevation reference
        /// for this ridge point (the FARTHER of the adjacent groups), or -1 if
        /// not a ridge point.
        /// </summary>
        public int RidgeReferenceGroupIndex { get; set; } = -1;
    }
}

