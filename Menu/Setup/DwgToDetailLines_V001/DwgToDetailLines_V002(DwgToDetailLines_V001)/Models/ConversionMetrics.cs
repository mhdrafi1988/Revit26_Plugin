namespace Revit26_Plugin.DwgToDetailLines.V002.Models
{
    /// <summary>
    /// Backs the Metrics Card in the UI.
    /// LayersFound / Entities populate on CAD import selection.
    /// Placed / Skipped populate after conversion runs (null = "—").
    /// </summary>
    public class ConversionMetrics
    {
        public int LayersFound { get; set; }
        public int Entities { get; set; }
        public int? Placed { get; set; }
        public int? Skipped { get; set; }

        public static ConversionMetrics Empty => new ConversionMetrics
        {
            LayersFound = 0,
            Entities = 0,
            Placed = null,
            Skipped = null
        };
    }
}
