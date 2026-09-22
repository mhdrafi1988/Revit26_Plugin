using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.ParaManager.V003.Models
{
    /// <summary>
    /// One row in the Assignment Queue DataGrid — one row per SharedParameterInfo,
    /// carrying the list of target categories it will be bound to.
    /// IsSelected drives the checkbox column (TwoWay) and participates in
    /// Select All / Clear Selection scoped to the filtered view.
    /// </summary>
    public partial class ParameterAssignmentRow : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        /// <summary>The shared parameter this row represents.</summary>
        public SharedParameterInfo Parameter { get; }

        /// <summary>Parameter Group name — displayed as its own grid column.</summary>
        public string ParameterGroup => Parameter.ParameterGroup;

        /// <summary>Parameter display name — displayed as its own grid column.</summary>
        public string ParameterName => Parameter.Name;

        /// <summary>Target categories this parameter will be assigned to (comma-joined for display).</summary>
        public List<CategoryInfo> Categories { get; }

        /// <summary>Comma-joined category names for the grid's "Category" column.</summary>
        public string CategoryDisplay => string.Join(", ", Categories.Select(c => c.Name));

        /// <summary>Populated after a Run: Instance / Type / (blank until run).</summary>
        [ObservableProperty]
        private string _bindingTypeDisplay = string.Empty;

        /// <summary>Properties-palette group this parameter is bound under in Revit.
        /// Starts as the auto-mapped value from the file's group name; editable per row
        /// (grid combo) or in bulk (Apply to Selected).</summary>
        [ObservableProperty]
        private RevitGroupOption _targetGroup;

        public ParameterAssignmentRow(SharedParameterInfo parameter, List<CategoryInfo> categories)
        {
            Parameter = parameter;
            Categories = categories;
            _targetGroup = RevitGroupOption.FromFileGroupName(parameter.ParameterGroup);
        }
    }
}
