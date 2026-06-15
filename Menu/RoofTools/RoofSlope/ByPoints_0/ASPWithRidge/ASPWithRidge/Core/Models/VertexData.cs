// =======================================================
// File: VertexData.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes:
//   + ElevationFromModel_mm  — elevation re-read from roof
//     vertex after tx.Commit(). What Revit actually stored.
//   + ElevationDiff_mm       — model minus calculated.
//     Zero = Revit accepted exactly. Non-zero = adjusted.
//   + IsRidgePoint           — true when vertex sits between
//     two or more drains and gets longest-path elevation.
//   + RidgeDrainA / B        — drain indices that bracket
//     the ridge (populated only when IsRidgePoint = true).
//   + RidgePathA_m / B_m     — the two path lengths that
//     triggered ridge detection (for Excel audit trail).
// =======================================================

using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models
{
    public class VertexData
    {
        public int VertexIndex { get; set; }
        public XYZ Position { get; set; }
        public double PathLengthMeters { get; set; }

        // Elevation calculated from: PathLength x SlopePercent
        // This is what the engine WROTE to the vertex.
        private double _elevationOffsetMm;
        public double ElevationOffsetMm
        {
            get => Math.Round(_elevationOffsetMm, 0);
            set => _elevationOffsetMm = value;
        }

        // Elevation READ BACK from the roof vertex after tx.Commit().
        // Reflects what Revit actually stored in the model.
        private double _elevationFromModel_mm;
        public double ElevationFromModel_mm
        {
            get => Math.Round(_elevationFromModel_mm, 0);
            set => _elevationFromModel_mm = value;
        }

        // Difference: model value minus calculated value.
        // Zero = Revit accepted exactly what was written.
        public double ElevationDiff_mm => ElevationFromModel_mm - ElevationOffsetMm;

        // ── Ridge fields ─────────────────────────────────────────────────────

        /// <summary>
        /// True when this vertex was detected as a ridge point — it sits
        /// between two or more drains and was assigned the longest path
        /// elevation instead of the shortest.
        /// </summary>
        public bool IsRidgePoint { get; set; }

        /// <summary>
        /// Index into finalDrainPoints for the first drain of the ridge pair.
        /// -1 when IsRidgePoint is false.
        /// </summary>
        public int RidgeDrainA { get; set; } = -1;

        /// <summary>
        /// Index into finalDrainPoints for the second drain of the ridge pair.
        /// -1 when IsRidgePoint is false.
        /// </summary>
        public int RidgeDrainB { get; set; } = -1;

        /// <summary>Path length to RidgeDrainA in meters (0 when not a ridge).</summary>
        public double RidgePathA_m { get; set; }

        /// <summary>Path length to RidgeDrainB in meters (0 when not a ridge).</summary>
        public double RidgePathB_m { get; set; }

        public int NearestDrainIndex { get; set; }
        public XYZ DirectionVector { get; set; }
        public bool WasProcessed { get; set; }

        public string Direction =>
            DirectionVector != null ?
            $"{DirectionVector.X:F3},{DirectionVector.Y:F3},{DirectionVector.Z:F3}" :
            "0,0,0";
    }
}
