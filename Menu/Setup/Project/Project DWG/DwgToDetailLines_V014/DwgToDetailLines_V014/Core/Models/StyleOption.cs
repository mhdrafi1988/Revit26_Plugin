using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Models
{
    /// <summary>
    /// One project line style or filled region type in a shortlist list (ADDED in V014).
    /// <see cref="IsShortlisted"/> marks the entries the auto-mapper may use.
    /// </summary>
    public partial class StyleOption : ObservableObject
    {
        /// <summary>Line style (OST_Lines subcategory) or FilledRegionType name.</summary>
        public string Name { get; init; }

        /// <summary>Colour / lineweight / pattern used for auto-matching.</summary>
        public CadLayerAppearance Appearance { get; init; }

        /// <summary>Swatch brush for the list.</summary>
        public Brush Swatch { get; init; }

        /// <summary>Short description for the list (lineweight and pattern, or fill pattern name).</summary>
        public string Detail { get; init; }

        [ObservableProperty] private bool isShortlisted;
    }
}
