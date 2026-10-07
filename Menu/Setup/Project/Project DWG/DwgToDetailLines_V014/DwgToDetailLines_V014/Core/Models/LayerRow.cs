using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
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
        /// Shortlist mode: the line style / filled region type used at Convert
        /// time (editable). Layer Name mode: the layer name (the style / type
        /// looked up, or created through a prompt, at Convert time).
        /// </summary>
        [ObservableProperty] private string resolvedStyleName;

        /// <summary>True when the row's style can be picked from the shortlist (ADDED in V014).</summary>
        [ObservableProperty] private bool isStyleEditable;

        /// <summary>Choices for the row's style dropdown: the line or hatch shortlist (ADDED in V014).</summary>
        [ObservableProperty] private IList<string> styleChoices;
    }
}
