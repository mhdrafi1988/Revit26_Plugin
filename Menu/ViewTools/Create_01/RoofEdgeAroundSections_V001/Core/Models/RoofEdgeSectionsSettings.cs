namespace Revit26_Plugin.RoofEdgeSections.V001
{
    /// <summary>
    /// Persisted user settings for RoofEdgeSections V001.
    /// Serialized to %AppData%\Revit26_Plugin\RoofEdgeSections\settings.json via System.Text.Json.
    /// Loaded on window open, saved on window close and after Run.
    /// </summary>
    public class RoofEdgeSectionsSettings
    {
        /// <summary>Offset distance from the roof edge to the section line, in millimeters.</summary>
        public double OffsetMm { get; set; } = 300;

        /// <summary>Section far clip depth, in millimeters.</summary>
        public double SectionDepthMm { get; set; } = 3000;

        /// <summary>Section crop box height, in millimeters.</summary>
        public double CropHeightMm { get; set; } = 2400;

        /// <summary>Crop width strategy: "TightToEdgeSpan" or "FixedWidth".</summary>
        public string CropWidthMode { get; set; } = "TightToEdgeSpan";

        /// <summary>Fixed crop width in millimeters, used only when CropWidthMode == "FixedWidth".</summary>
        public double FixedCropWidthMm { get; set; } = 5000;

        /// <summary>Name of the View Template to apply to created sections, or "None".</summary>
        public string ViewTemplateName { get; set; } = "None";

        /// <summary>Post-creation behavior: "AskMe", "OpenAll", or "DontOpen".</summary>
        public string OpenViewsMode { get; set; } = "AskMe";

        /// <summary>Last folder used for manual log export, reused for the session.</summary>
        public string LastLogExportFolder { get; set; } = "";
    }
}
