using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Models
{
    /// <summary>
    /// One project line style in the "Line Style Shortlist" list (ADDED in V014).
    /// <see cref="IsShortlisted"/> marks the styles the auto-mapper may use.
    /// </summary>
    public partial class LineStyleOption : ObservableObject
    {
        /// <summary>Line style (OST_Lines subcategory) name.</summary>
        public string Name { get; init; }

        /// <summary>Colour / lineweight / pattern used for auto-matching.</summary>
        public CadLayerAppearance Appearance { get; init; }

        /// <summary>Swatch brush for the list.</summary>
        public Brush Swatch { get; init; }

        /// <summary>Short "LW n · solid/pattern" description for the list.</summary>
        public string Detail { get; init; }

        [ObservableProperty] private bool isShortlisted;
    }
}
