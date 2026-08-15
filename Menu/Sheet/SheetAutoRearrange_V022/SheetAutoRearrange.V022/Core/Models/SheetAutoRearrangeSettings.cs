namespace Revit26_Plugin.SheetAutoRearrange.V022.Core.Models
{
    /// <summary>
    /// Everything persisted to %AppData%\Revit26_Plugin\SheetAutoRearrange\settings.json,
    /// per suite convention. Loaded on window open, saved on window close and
    /// after each Run.
    ///
    /// V012 CHANGE: adds ColumnToleranceMm and WithinColumnAlign — NEW
    /// FIELDS ONLY, PERSISTED BUT NOT YET WIRED INTO ANY PACKING LOGIC.
    /// A true "Column Tolerance" mirroring Row Tolerance would require a
    /// second, parallel column-based packing algorithm (grouping views into
    /// vertical columns by X-proximity, mirroring how Row Tolerance groups
    /// into horizontal rows by Y-proximity) — that's substantial new
    /// algorithm work that was explicitly NOT confirmed as in-scope for this
    /// version. These two fields exist in the UI and persist correctly, but
    /// currently have NO EFFECT on placement. Flagged clearly — do not
    /// assume they do anything yet.
    ///
    /// V014 CHANGE: TallDetectionSettings/WideDetectionSettings REMOVED.
    /// The Tall/Wide anchor + L-shape shelf system (ShelfPackingService,
    /// ViewSizeClassifierService, ViewSizeCategory, ShelfBlock) is removed
    /// entirely per explicit request, replaced by MasterRowBandPackingService
    /// — a uniform Master Row + height-Band system with three selectable
    /// fill strategies (RowFillStrategy: Shelf/Guillotine/MaxRects). There is
    /// no anchor concept in the new system; every item, regardless of size,
    /// goes through the same Master Row/Band pipeline. Ported from the
    /// user-supplied algorithm spec EXACTLY AS WRITTEN, including a known
    /// row-height plateau behavior (Master Row height = tallest item still
    /// anywhere unplaced, not tallest-in-this-row) — left unfixed per
    /// explicit request ("port as-specified, I want to test it myself").
    ///
    /// V015 CHANGE: SheetOrderPackingService now actually relocates
    /// overflow items (Fits=false) below the titleblock's usable area
    /// instead of leaving them at their originally-computed (possibly
    /// in-bounds/overlapping) coordinates — see that class's remarks for
    /// the relocation logic. Also: MasterRowBandPackingService internals
    /// refactored (StartMasterRow / PackColumnsIntoRow helpers extracted
    /// from previously-duplicated code in PackShelf/PackFreeRect) —
    /// behavior-preserving, verified via parallel Python simulation
    /// producing identical placement counts before/after. No settings
    /// schema changes in V015 — this class is unchanged from V014.
    ///
    /// V021 CHANGE: SheetOrderPackingService internals refactored —
    /// extracted WFeet/HFeet (item.WidthMm/HeightMm * MmToFeet, previously
    /// repeated inline at 8+ call sites) and GetBounds (center + half-size
    /// -> left/right/top/bottom, previously duplicated once for the
    /// overlap-check candidate and once inline in the accepted-items
    /// lambda) as shared private helpers. Behavior-preserving — re-verified
    /// via the same parallel Python simulation, identical placement counts.
    /// No settings schema changes — this class is unchanged from V015.
    ///
    /// V021 CLEANUP (2): Confirmed-dead code removed outright —
    /// RearrangeAlgorithm.cs (unused enum), ReadingOrderPackingService.cs
    /// (unused since V009, per RearrangeEngine's remarks), and
    /// PlaceableRegion.SmallRect/IsLShape/RectFeet.ContainsYBand (vestigial
    /// since V008, dead since V014 — all 3 constructor call sites always
    /// passed null). PlaceableRegion's constructor signature changed
    /// accordingly (smallRect parameter removed) — all 3 call sites in
    /// TitleBlockDetectionService updated to match. ColumnToleranceMm/
    /// WithinColumnAlign intentionally NOT touched — still persisted, still
    /// not wired into packing logic, per explicit instruction to leave
    /// those alone.
    ///
    /// V022 CHANGE: RowFillStrategy grows from 3 to 7 members (MaxFill,
    /// RafisAlgo, Skyline, SkylineWasteMap added — see that enum and the
    /// new FreeRectPackingService/RafisAlgoPackingService). Default changed
    /// Shelf -> MaxFill per explicit spec. System.Text.Json serializes
    /// enums by name by default, so no schema/converter change needed —
    /// any settings.json saved under V021 with RowFillStrategy still set
    /// to one of the original 3 names deserializes unchanged.
    /// </summary>
    public class SheetAutoRearrangeSettings
    {
        public GapSettings GapSettings { get; set; } = new();

        /// <summary>V014 NEW, V022 CHANGE — which of the 7 fill strategies to use (3 band-based + 4 free-packing). Default MaxFill per V022 spec (was Shelf).</summary>
        public RowFillStrategy RowFillStrategy { get; set; } = RowFillStrategy.MaxFill;

        public double RowToleranceMm { get; set; } = 50;
        public RowAlignment RowAlignment { get; set; } = RowAlignment.Bottom;

        /// <summary>V012 NEW — persisted placeholder, no packing effect yet. See class remarks.</summary>
        public double ColumnToleranceMm { get; set; } = 50;

        /// <summary>V012 NEW — persisted placeholder, no packing effect yet. See class remarks.</summary>
        public BlockAlignmentH WithinColumnAlign { get; set; } = BlockAlignmentH.Right;

        public BlockAlignmentH ColumnAlignH { get; set; } = BlockAlignmentH.Left;
        public BlockAlignmentV ColumnAlignV { get; set; } = BlockAlignmentV.Top;

        public OverflowHandlingMode OverflowHandlingMode { get; set; } = OverflowHandlingMode.PlaceWhatsPlaceable;
    }
}
