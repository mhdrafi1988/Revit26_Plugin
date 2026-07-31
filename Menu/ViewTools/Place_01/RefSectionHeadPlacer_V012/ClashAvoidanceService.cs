using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RefSectionHeadPlacer.V012.Core.Services
{
    /// <summary>
    /// Checks a candidate section origin against section heads already placed
    /// earlier in the SAME run, and nudges the point in an expanding radial
    /// search until clear. Per Rafi's confirmed scope, this checks ONLY this
    /// run's placements — not pre-existing model annotations. Currently applied
    /// to the Wall category, the only one whose origin can legitimately clash
    /// (corner-based, not per-element unique like LocationPoint/centroid).
    ///
    /// Clearance is measured in PLAN (XY) only: the reference heads are drawn in
    /// a plan view, so two heads sharing an XY position overlap visually even if
    /// their model Z differs (e.g. multi-level roofs). A 3D distance would miss
    /// that and leave them stacked in the drawing.
    /// </summary>
    public class ClashAvoidanceService
    {
        private const double ClearanceRadius = 1.0;   // feet — min spacing between heads
        private const double NudgeStep = 0.5;         // feet — radial search step
        private const int MaxAttempts = 12;           // ~2 full radial passes before giving up

        private readonly List<XYZ> _placedOrigins = new List<XYZ>();

        /// <summary>Registers a successfully placed section's origin so later
        /// candidates in this run are checked against it.</summary>
        public void RegisterPlacement(XYZ origin) => _placedOrigins.Add(origin);

        /// <summary>V012: public clash test for the engine's wall candidate loop
        /// (mid → ¼ → ¾). Same XY / this-run-only rule as everything else here.</summary>
        public bool IsClear(XYZ point) => !IsClashing(point);

        /// <summary>
        /// Returns a clash-free origin near the candidate point, or null if no
        /// clear position was found within MaxAttempts — caller should skip
        /// and log a Warning in that case (per-item SubTransaction pattern).
        /// </summary>
        public XYZ ResolveClashFreeOrigin(XYZ candidate)
        {
            if (!IsClashing(candidate))
                return candidate;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                double angle = (2 * System.Math.PI / 8) * (attempt % 8);
                double radius = NudgeStep * ((attempt / 8) + 1);

                XYZ nudged = candidate + new XYZ(
                    radius * System.Math.Cos(angle),
                    radius * System.Math.Sin(angle),
                    0);

                if (!IsClashing(nudged))
                    return nudged;
            }

            return null; // unresolved — caller skips and logs Warning
        }

        private bool IsClashing(XYZ point)
        {
            foreach (var placed in _placedOrigins)
            {
                // Plan (XY) distance — Z is ignored on purpose (see class summary).
                double dx = point.X - placed.X;
                double dy = point.Y - placed.Y;
                if (System.Math.Sqrt(dx * dx + dy * dy) < ClearanceRadius)
                    return true;
            }
            return false;
        }
    }
}
