using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Models
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
        /// <summary>CAD layer name.</summary>
        public string LayerName { get; init; }

        /// <summary>Line or Hatch.</summary>
        public CadEntityType EntityType { get; init; }

        /// <summary>Number of entities on this layer.</summary>
        public int Count { get; init; }

        /// <summary>CAD layer colour / lineweight / pattern, or null when unknown (ADDED in V014).</summary>
        [ObservableProperty] private CadLayerAppearance appearance;

        /// <summary>Swatch of the CAD layer colour (ADDED in V014).</summary>
        [ObservableProperty] private Brush swatch;

        [ObservableProperty] private bool isSelected = true;

        /// <summary>
        /// Style / pattern shown in the grid's "Style / Pattern" column.
        /// Line rows in Shortlist mode: the line style used at Convert time
        /// (editable). Line rows in Layer Name mode: the layer name.
        /// Hatch rows: display of the default fill pattern; GetOrResolve still
        /// does the real per-layer FilledRegionType lookup at Convert time.
        /// </summary>
        [ObservableProperty] private string resolvedStyleName;

        /// <summary>True when the row's style can be picked from the shortlist (ADDED in V014).</summary>
        [ObservableProperty] private bool isStyleEditable;
    }
}
