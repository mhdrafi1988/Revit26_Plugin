using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofCreationIsolationTest.V001.Core.Models
{
    /// <summary>
    /// Captures the full outcome of a single NewFootPrintRoof() isolation attempt —
    /// inputs used, success/failure, and (on failure) full exception detail.
    /// Purely a data carrier; no logic. Consumed by the handler to drive log output
    /// and by the ViewModel for the completion summary line.
    /// </summary>
    public class RoofTestResult
    {
        /// <summary>True if NewFootPrintRoof() returned a valid FootPrintRoof without throwing.</summary>
        public bool Success { get; set; }

        // ── Pre-call validation (runs before NewFootPrintRoof(); does NOT block execution) ──
        /// <summary>True if every validation check passed. False does not prevent the API call.</summary>
        public bool ValidationPassed { get; set; }

        /// <summary>One entry per failed check, human-readable. Empty if ValidationPassed is true.</summary>
        public System.Collections.Generic.List<string> ValidationIssues { get; set; } = new();

        // ── Inputs actually used (captured regardless of outcome) ──────────────
        public ElementId? LevelId { get; set; }
        public string? LevelName { get; set; }
        public double LevelElevationFt { get; set; }

        public ElementId? RoofTypeId { get; set; }
        public string? RoofTypeName { get; set; }

        /// <summary>Footprint corner points in feet (internal units), in order.</summary>
        public XYZ[]? FootprintPointsFt { get; set; }

        // ── Success output ──────────────────────────────────────────────────────
        public ElementId? CreatedRoofId { get; set; }
        public string? CreatedRoofName { get; set; }

        // ── Failure output — full diagnostic detail, never truncated ───────────
        public string? ExceptionTypeName { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? ExceptionStackTrace { get; set; }
        public string? ExceptionSource { get; set; }

        /// <summary>Full inner-exception chain, each entry already formatted (type/message/stack).</summary>
        public System.Collections.Generic.List<string> InnerExceptionChain { get; set; } = new();
    }
}
