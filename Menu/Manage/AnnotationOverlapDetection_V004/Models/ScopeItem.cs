using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Models
{
    /// <summary>
    /// One row in the batch picker (v4.2.0): a sheet or a view, with a checkbox.
    /// </summary>
    public partial class ScopeItem : ObservableObject
    {
        /// <summary>Element id of the sheet or view.</summary>
        public long Id { get; init; }

        /// <summary>Main text, e.g. "A101 - Ground Floor" or a view name.</summary>
        public string Name { get; init; }

        /// <summary>Secondary text, e.g. "3 views" or "Section".</summary>
        public string Detail { get; init; }

        /// <summary>For a sheet: ids of the supported views placed on it.</summary>
        public long[] ViewIds { get; init; } = System.Array.Empty<long>();

        [ObservableProperty]
        private bool isSelected;
    }
}
