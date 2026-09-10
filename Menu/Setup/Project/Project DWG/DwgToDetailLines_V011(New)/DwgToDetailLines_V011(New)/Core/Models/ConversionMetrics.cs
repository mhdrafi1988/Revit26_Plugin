using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.DwgToDetailLines.V011.Core.Models
{
    /// <summary>
    /// Backs the Metrics Card in the UI.
    /// LayersFound / Entities populate on CAD import selection.
    /// Placed / Skipped / Failed populate after conversion runs (null = "—").
    /// Skipped = pre-filtered (short curve, user chose not to create layer style).
    /// Failed = passed pre-filter but threw on NewDetailCurve (unexpected geometry issue).
    /// </summary>
    public partial class ConversionMetrics : ObservableObject
    {
        [ObservableProperty] private int layersFound;
        [ObservableProperty] private int entities;
        [ObservableProperty] private int? placed;
        [ObservableProperty] private int? skipped;
        [ObservableProperty] private int? failed;

        public static ConversionMetrics Empty => new ConversionMetrics
        {
            LayersFound = 0,
            Entities = 0,
            Placed = null,
            Skipped = null,
            Failed = null
        };
    }
}
