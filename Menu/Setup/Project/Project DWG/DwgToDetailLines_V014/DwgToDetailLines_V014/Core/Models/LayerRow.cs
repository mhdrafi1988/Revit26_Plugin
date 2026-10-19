using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

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
        /// <summary>
        /// Dropdown entry meaning "use the V013 behaviour": pick the line style / filled region
        /// type named after the CAD layer, and prompt to create it when it is missing.
        /// </summary>
        public const string MatchLayerName = "(Match layer name)";

        private bool _suppressOverride;

        public string LayerName { get; init; }
        public CadEntityType EntityType { get; init; }
        public int Count { get; init; }

        /// <summary>Choices offered in this row's dropdown (line styles for lines, fill types for hatches).</summary>
        public IReadOnlyList<string> Options { get; set; } = new List<string> { MatchLayerName };

        /// <summary>
        /// The user's own pick for this row in Multiple mode, or null while the row follows the
        /// global value.
        /// </summary>
        public string Override { get; set; }

        [ObservableProperty] private bool isSelected = true;

        /// <summary>
        /// Style / type currently shown in (and, in Multiple mode, chosen with) the row's dropdown.
        /// Editing it by hand records an <see cref="Override"/>.
        /// </summary>
        [ObservableProperty] private string shownStyle = MatchLayerName;

        partial void OnShownStyleChanged(string value)
        {
            if (!_suppressOverride)
                Override = value;
        }

        /// <summary>Sets the displayed value from code without recording it as a user override.</summary>
        public void ShowWithoutOverride(string value)
        {
            _suppressOverride = true;
            ShownStyle = value;
            _suppressOverride = false;
        }
    }
}
