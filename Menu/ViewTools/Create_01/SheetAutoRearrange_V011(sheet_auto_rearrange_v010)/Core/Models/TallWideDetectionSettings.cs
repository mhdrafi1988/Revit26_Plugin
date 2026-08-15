using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SheetAutoRearrange.V011.Core.Models
{
    /// <summary>
    /// One axis's detection settings — used twice (Tall = height axis,
    /// Wide = width axis), each fully independent per confirmed design:
    /// separate Enable, Multiplier, Tolerance, and OverflowGrouping per axis.
    /// Persisted as part of the tool's settings.json.
    ///
    /// V010 CHANGE: adds SubRowAlignment and SubColumnAlignment — previously
    /// hardcoded (top-aligned within a Tall anchor's sub-rows, left-aligned
    /// within a Wide anchor's sub-columns) in ShelfPackingService, now
    /// user-configurable per explicit request. Both fields exist on this
    /// single shared class (used for both Tall and Wide instances) rather
    /// than splitting into two classes — only ONE of the two fields is
    /// meaningful per instance (SubRowAlignment for the Tall instance,
    /// SubColumnAlignment for the Wide instance; the other sits unused on
    /// that instance). This is a minor structural compromise to avoid
    /// duplicating the rest of the class (Enable/Multiplier/Tolerance/
    /// OverflowGrouping) across two near-identical types.
    /// </summary>
    public partial class TallWideDetectionSettings : ObservableObject
    {
        [ObservableProperty] private bool isEnabled = true;

        /// <summary>View is flagged when its size ≥ (mode × Multiplier), within ± TolerancePercent. Default 2.0 per confirmed spec.</summary>
        [ObservableProperty] private double multiplier = 2.0;

        /// <summary>Tolerance band around the multiplier threshold, as a percentage. Default 10.</summary>
        [ObservableProperty] private double tolerancePercent = 10.0;

        [ObservableProperty] private ShelfOverflowGrouping overflowGrouping = ShelfOverflowGrouping.KeepShelfTogether;

        /// <summary>
        /// V010 NEW: only meaningful on the TALL-axis instance. Alignment of
        /// items WITHIN each sub-row packed beside a Tall (or TallAndWide)
        /// anchor. Default Bottom, matching the person's stated overall
        /// preference ("most of my alignment are bottom for rows").
        /// </summary>
        [ObservableProperty] private RowAlignment subRowAlignment = RowAlignment.Bottom;

        /// <summary>
        /// V010 NEW: only meaningful on the WIDE-axis instance. Alignment of
        /// items WITHIN each sub-column packed beside a Wide (or TallAndWide)
        /// anchor. Default Right, matching the person's stated overall
        /// preference ("right for columns").
        /// </summary>
        [ObservableProperty] private BlockAlignmentH subColumnAlignment = BlockAlignmentH.Right;
    }
}
