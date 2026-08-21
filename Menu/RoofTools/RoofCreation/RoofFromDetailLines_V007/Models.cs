using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofFromDetailLines.V007
{
    /// <summary>How a loop was classified during processing — drives both the grid's
    /// status pill and which copyable text area (if any) its source line IDs land in.</summary>
    public enum LoopStatus
    {
        Ready,          // already closed, no modification needed
        CornerTrimmed,  // closed after extend/trim corner clean-up and/or overlap resolution
        AutoClosed,     // closed by inserting a straight connector line between 2 free endpoints
        Opening,        // valid loop that will be used as an opening inside an outer roof
        Skipped         // unresolvable — not included in any roof
    }

    /// <summary>One resolved closed loop, prior to nesting/pairing into roofs.</summary>
    public class LoopCandidate
    {
        public int SerialNumber { get; set; }
        public IList<Curve> Curves { get; set; }
        public LoopStatus Status { get; set; }

        /// <summary>Element IDs of the ORIGINAL detail lines/arcs that contributed to this
        /// loop (before any auto-close connector or corner-extension was added).</summary>
        public List<ElementId> SourceLineIds { get; set; } = new();

        /// <summary>True if this loop required inserting a brand-new connector line
        /// (auto-close). Its SourceLineIds are excluded from deletion even when the
        /// user opts to delete source lines, per confirmed spec.</summary>
        public bool WasAutoClosed { get; set; }

        /// <summary>True if this loop required extend/trim corner clean-up and/or
        /// overlap resolution (both use the same extend-to-junction mechanism).</summary>
        public bool WasCornerTrimmed { get; set; }

        public double AreaInternal { get; set; } // square feet (Revit internal units)

        /// <summary>Signed area sign is used to determine outer vs inner (CW/CCW) —
        /// populated by the containment/nesting pass.</summary>
        public bool IsClockwise { get; set; }

        /// <summary>Bounding box (model XY, internal units) — used for fast containment
        /// pre-checks before doing a full point-in-polygon test.</summary>
        public BoundingBoxXYZ BoundingBox { get; set; }
    }

    /// <summary>Why a group of connected lines could not be resolved into a loop.</summary>
    public class SkippedGroup
    {
        public List<ElementId> LineIds { get; set; } = new();
        public string Reason { get; set; }
    }

    /// <summary>One outer boundary + its (possibly empty) set of opening loops — the
    /// unit that maps 1:1 to a single FootPrintRoof.</summary>
    public class RoofGroup
    {
        public int RoofNumber { get; set; }
        public LoopCandidate Outer { get; set; }
        public List<LoopCandidate> Openings { get; set; } = new();

        public double AreaDisplay { get; set; } // converted to sq m or project units for grid display

        public bool ContainsAutoClosed =>
            Outer.WasAutoClosed || Openings.Exists(o => o.WasAutoClosed);

        public bool ContainsCornerTrimmed =>
            Outer.WasCornerTrimmed || Openings.Exists(o => o.WasCornerTrimmed);
    }

    /// <summary>Per-run tallies shown on the summary line and logged.</summary>
    public class RunSummary
    {
        public int RoofsCreatedCount { get; set; }
        public int AutoClosedLoopCount { get; set; }
        public int CornerTrimmedLoopCount { get; set; }
        public int SkippedGroupCount { get; set; }
        public int FailedCount { get; set; }
        public int SourceLinesDeletedCount { get; set; }
    }

    /// <summary>Everything the ExternalEvent handler needs to run one pass.</summary>
    public class CreateRoofsRunRequest
    {
        public List<RoofGroup> RoofGroups { get; set; }
        public ElementId RoofTypeId { get; set; }
        public Level Level { get; set; }
        public bool DeleteSourceLines { get; set; }
    }

    /// <summary>Per-tool persisted settings (System.Text.Json POCO), stored at
    /// %AppData%\Revit26_Plugin\RoofFromDetailLines\settings.json.</summary>
    public class ToolSettings
    {
        public string LastRoofTypeName { get; set; }
        public double CornerToleranceMm { get; set; } = 100.0;
        public bool DeleteSourceLines { get; set; } = false;
    }
}
