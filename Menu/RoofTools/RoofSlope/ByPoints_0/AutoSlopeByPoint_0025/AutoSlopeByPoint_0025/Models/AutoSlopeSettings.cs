// =======================================================
// File: AutoSlopeSettings.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V025
// New in V025.
// Purpose: Plain POCO persisted to
//          %AppData%\Revit26_Plugin\AutoSlopeByPoint\settings.json
//          via System.Text.Json.
// Scope: This tool had no settings persistence before V025 — only
//        the new Circle Marker fields are persisted for now (radius,
//        color, threshold, and line-style NAME — the ElementId is
//        project-specific and not portable across documents, so only
//        the name is saved and re-matched against LineStyleOptions
//        on load; if no match is found the constructor's default
//        line style applies instead).
// =======================================================

namespace Revit26_Plugin.AutoSlopeByPoint.V025.Core.Models
{
    public class CircleMarkerGroupSettings
    {
        public bool IsEnabled { get; set; } = true;
        public string LineStyleName { get; set; }
        public string ColorName { get; set; } = "Black";
        public double RadiusMm { get; set; } = 500;
    }

    public class AutoSlopeSettings
    {
        public CircleMarkerGroupSettings DrainMarkerGroup { get; set; } = new CircleMarkerGroupSettings();
        public CircleMarkerGroupSettings HighestPointMarkerGroup { get; set; } = new CircleMarkerGroupSettings();
        public CircleMarkerGroupSettings AllowedOffsetMarkerGroup { get; set; } = new CircleMarkerGroupSettings();
        public double AllowedOffsetThresholdMm { get; set; } = 500;
    }
}
