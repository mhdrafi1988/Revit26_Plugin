using System.Collections.Generic;

namespace Revit26_Plugin.RoofEdgeSections.V002
{
    /// <summary>
    /// Persisted user settings for RoofEdgeSections V002.
    /// Serialized to %AppData%\Revit26_Plugin\RoofEdgeSections\settings.json via System.Text.Json.
    /// Loaded on window open, saved on window close and after Run.
    ///
    /// V001 → V002 schema change: SectionDepthMm, CropWidthMode, FixedCropWidthMm removed
    /// (crop width is now always dynamic, based on nearby wall thickness). Old settings.json
    /// files deserialize cleanly — removed fields are dropped, new fields take their defaults
    /// below. No migration step needed.
    /// </summary>
    public class RoofEdgeSectionsSettings
    {
        /// <summary>Offset distance from the roof edge to the section origin, in millimeters.</summary>
        public double OffsetMm { get; set; } = 300;

        /// <summary>
        /// How far to search inward from the edge (along InwardNormal, from the offset origin)
        /// to find a nearby wall, in millimeters. Also used as the fallback crop width
        /// (perpendicular to the edge) when no wall is found within this distance.
        /// </summary>
        public double SearchDistanceMm { get; set; } = 1000;

        /// <summary>
        /// Extra distance added past a found wall's far face, in millimeters, so the crop
        /// shows a margin beyond the wall rather than cutting flush at its face.
        /// </summary>
        public double MarginOffsetMm { get; set; } = 150;

        /// <summary>
        /// How much of the roof edge (along its length/tangent direction) is visible in the
        /// section, in millimeters. Used both as the crop width along the edge tangent and
        /// as the camera's far-clip depth (single value serves both roles — confirmed).
        /// </summary>
        public double EdgeDepthMm { get; set; } = 1000;

        /// <summary>Section crop box height, in millimeters.</summary>
        public double CropHeightMm { get; set; } = 2400;

        /// <summary>Name of the View Template to apply to created sections, or "None".</summary>
        public string ViewTemplateName { get; set; } = "None";

        /// <summary>Post-creation behavior: "AskMe", "OpenAll", or "DontOpen".</summary>
        public string OpenViewsMode { get; set; } = "AskMe";

        /// <summary>
        /// When true, planned sections whose edge midpoints fall within MergeDistanceMm of an
        /// already-kept section are discarded (first-found kept). Applies across all selected
        /// roofs, not just within a single roof.
        /// </summary>
        public bool MergeEnabled { get; set; } = true;

        /// <summary>Proximity-merge threshold, in millimeters, measured between edge midpoints.</summary>
        public double MergeDistanceMm { get; set; } = 500;

        /// <summary>
        /// Ordered, toggleable tokens used to build each section view name.
        /// See <see cref="NamingToken"/> for the token set and default order.
        /// </summary>
        public List<NamingToken> NamingTokens { get; set; } = NamingToken.Defaults();

        /// <summary>Separator character(s) inserted between enabled naming tokens.</summary>
        public string NamingSeparator { get; set; } = "_";

        /// <summary>Last folder used for manual log export, reused for the session.</summary>
        public string LastLogExportFolder { get; set; } = "";
    }
}
