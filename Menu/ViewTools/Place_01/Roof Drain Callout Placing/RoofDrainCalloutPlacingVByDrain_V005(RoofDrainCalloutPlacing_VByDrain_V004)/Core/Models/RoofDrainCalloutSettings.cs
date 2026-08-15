using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.Models
{
    /// <summary>
    /// VByDrain.V004 settings — persisted to %AppData%\Revit26_Plugin\RoofDrainCalloutPlacing\VByDrain.V004\settings.json
    ///
    /// V004 changes from V002:
    /// - Removed: global CalloutOffsetMm, CalloutMarginMm, CalloutFloorMm
    /// - Added: GroupSizing — per shape-group (Circle, Rectangle, Other) Auto/Fixed sizing config.
    ///   Auto mode: bounding box of that group's selected openings + Margin on all sides.
    ///   Fixed mode: fixed square box (FixedSize x FixedSize), regardless of opening/group size.
    /// - Kept: drafting view selection
    /// </summary>
    public class RoofDrainCalloutSettings
    {
        [JsonPropertyName("groupSizing")]
        public Dictionary<string, GroupSizingSettings> GroupSizing { get; set; } = new()
        {
            ["Circle"] = new GroupSizingSettings(),
            ["Rectangle"] = new GroupSizingSettings(),
            ["Other"] = new GroupSizingSettings()
        };

        [JsonPropertyName("draftingViewName")]
        public string DraftingViewName { get; set; } = "";

        [JsonPropertyName("lastRunSucceeded")]
        public bool LastRunSucceeded { get; set; } = false;

        [JsonPropertyName("lastRunTimestamp")]
        public string LastRunTimestamp { get; set; } = "";
    }

    /// <summary>
    /// Per-group callout sizing config. "auto" derives the callout box from the
    /// bounding box of that group's selected openings + Margin on every side.
    /// "fixed" always uses a FixedSize x FixedSize square box, centered on the
    /// group's selected-opening centroid, regardless of opening/group size.
    /// </summary>
    public class GroupSizingSettings
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "auto"; // "auto" | "fixed"

        [JsonPropertyName("margin")]
        public double Margin { get; set; } = 100;

        [JsonPropertyName("fixedSize")]
        public double FixedSize { get; set; } = 500;
    }
}
