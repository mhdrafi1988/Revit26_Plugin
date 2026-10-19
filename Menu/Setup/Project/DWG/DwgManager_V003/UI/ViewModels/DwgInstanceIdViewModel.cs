using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.DwgManager.V003.UI.ViewModels
{
    /// <summary>
    /// One element-ID chip in a row's ID panel. Clicking it selects and zooms to the instance, which is
    /// only possible while the instance is drawn in the active view.
    /// </summary>
    public partial class DwgInstanceIdViewModel : ObservableObject
    {
        /// <summary>Creates a chip for <paramref name="id"/>.</summary>
        public DwgInstanceIdViewModel(ElementId id)
        {
            Id = id;
            Label = $"#{id.Value}";
        }

        /// <summary>The CAD instance.</summary>
        public ElementId Id { get; }

        /// <summary>"#123456".</summary>
        public string Label { get; }

        /// <summary>True when the instance is drawn in the active view, so select + zoom works.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ToolTipText))]
        private bool isClickable;

        /// <summary>Tooltip explaining what a click does, or why it is disabled.</summary>
        public string ToolTipText => IsClickable
            ? "Select this instance and zoom to it"
            : "Not in the active view — open the view first";
    }
}
