namespace Revit26_Plugin.RoofViewFocus.V004.Core.Models
{
    /// <summary>
    /// Persisted to %AppData%\Revit26_Plugin\RoofViewFocus\settings.json.
    /// </summary>
    public class RoofViewFocusSettings
    {
        public double ViewMarginMm { get; set; } = RoofViewFocusDefaults.DefaultMarginMm;
        public double AnnotationMarginMm { get; set; } = RoofViewFocusDefaults.DefaultMarginMm;
    }
}
