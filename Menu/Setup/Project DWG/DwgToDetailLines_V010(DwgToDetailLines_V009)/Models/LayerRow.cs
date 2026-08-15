using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.DwgToDetailLines.V010.Models
{
    /// <summary>
    /// One row in the Layers &amp; Hatches To Convert grid.
    /// Built during CadGeometryExtractor.ScanLayers(), one row per
    /// distinct (Layer, EntityType) pair found in the CAD import.
    /// IsSelected drives the checkbox column (TwoWay); checked rows
    /// are the only ones passed to DetailLineConversionService.Execute().
    /// </summary>
    public partial class LayerRow : ObservableObject
    {
        public string LayerName { get; init; }
        public CadEntityType EntityType { get; init; }
        public int Count { get; init; }

        [ObservableProperty] private bool isSelected = true;

        /// <summary>
        /// Display-only resolved style/pattern name shown in the grid's
        /// "Style / Pattern" column. Set from the global default on scan;
        /// does not itself drive conversion — GetOrResolve still does the
        /// real per-layer subcategory/FilledRegionType lookup at Convert time.
        /// </summary>
        [ObservableProperty] private string resolvedStyleName;
    }
}
