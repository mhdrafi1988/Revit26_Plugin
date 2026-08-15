using System.Collections.Generic;

namespace Revit26_Plugin.SlopeDirections.V005
{
    /// <summary>
    /// UI-friendly container for analysis results.
    /// </summary>
    public sealed class RoofDrainageAnalysisDto
    {
        public RoofDrainageRunResult Summary { get; set; }
        public List<WaterPathDto> Paths { get; set; }
    }
}
