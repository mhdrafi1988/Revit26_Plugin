using Revit26_Plugin.RoofTag.V016.Helpers;

namespace Revit26_Plugin.RoofTag.V016
{
    /// <summary>
    /// Persisted to %AppData%\Revit26_Plugin\RoofTag_V016\settings.json.
    /// </summary>
    public class RoofTagSettings
    {
        public bool UseManualMode { get; set; } = false;
        public bool IsAngle45 { get; set; } = true;
        public bool BendInward { get; set; } = true;
        public double BendOffset { get; set; } = 1000.0;
        public double EndOffset { get; set; } = 2000.0;
        public bool UseLeader { get; set; } = true;

        public bool ClusterFilterEnabled { get; set; } = true;
        public double ClusterRadius { get; set; } = 600.0;

        public bool InteriorLoopReductionEnabled { get; set; } = true;
        public double InteriorLoopSpacing { get; set; } = 500.0;
        public double InteriorLoopBoundaryTolerance { get; set; } = 50.0;

        public bool ExteriorLoopReductionEnabled { get; set; } = true;
        public double ExteriorLoopSpacing { get; set; } = 500.0;
        public double ExteriorLoopBoundaryTolerance { get; set; } = 50.0;

        public string SelectedSpotTagTypeName { get; set; } = null;

        public RoofTagElevationFace ElevationFace { get; set; } = RoofTagElevationFace.Top;
    }
}
