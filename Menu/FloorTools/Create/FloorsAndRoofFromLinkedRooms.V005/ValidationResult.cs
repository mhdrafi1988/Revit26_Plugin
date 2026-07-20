namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005
{
    /// <summary>
    /// Shared result type returned by FloorBoundaryValidationService and
    /// RoofBoundaryValidationService. Carries pass/fail plus roof-specific
    /// inner-loop accounting so the handler can log and tally without
    /// recomputing anything itself.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>True if the boundary can be safely passed to Floor.Create / NewFootPrintRoof.</summary>
        public bool IsValid { get; set; }

        /// <summary>Populated only when IsValid is false — reason the boundary was rejected.</summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Count of inner loops dropped from the final element. Always 0 for floors
        /// (floors keep their holes). For roofs: (loops.Count - 1) valid inner loops
        /// that NewFootPrintRoof cannot accept, plus any inner loops that separately
        /// failed geometric validation upstream.
        /// </summary>
        public int InnerLoopsDropped { get; set; }

        /// <summary>
        /// Pre-built warning text for the handler to log when InnerLoopsDropped > 0,
        /// e.g. "2 inner loop(s) not supported for roofs, outer boundary used".
        /// Null when InnerLoopsDropped is 0. Does not include the room display name —
        /// the handler prepends that, matching the existing log line convention.
        /// </summary>
        public string InnerLoopsWarning { get; set; }
    }
}
