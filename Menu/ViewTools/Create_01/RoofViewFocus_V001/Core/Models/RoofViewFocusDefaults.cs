namespace Revit26_Plugin.RoofViewFocus.V001.Core.Models
{
    /// <summary>Tool-level constants. Pure .NET — no Revit API.</summary>
    public static class RoofViewFocusDefaults
    {
        public const string ToolName = "RoofViewFocus";
        public const string Version = "V001";
        public const string Title = "Roof View Focus — V001";

        /// <summary>Default value of both margin input fields (mm).</summary>
        public const double DefaultMarginMm = 20.0;

        /// <summary>Fixed offset (mm) added on top of the user-entered margin. Not editable in UI.</summary>
        public const double DefaultOffsetMm = 20.0;
    }
}
