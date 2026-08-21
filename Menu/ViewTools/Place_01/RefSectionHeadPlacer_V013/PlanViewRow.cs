using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RefSectionHeadPlacer.V013.Core.Models
{
    /// <summary>
    /// Bindable row representing a single plan view in Grid 1.
    /// Backed by the underlying Revit ViewPlan for later element collection.
    /// </summary>
    public partial class PlanViewRow : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        /// <summary>Display name shown in the grid (View.Name).</summary>
        public string ViewName { get; }

        /// <summary>View family type label (e.g., "Floor Plan").</summary>
        public string ViewType { get; }

        /// <summary>Associated level name, if any.</summary>
        public string Level { get; }

        /// <summary>Underlying Revit view element, used by ElementCollectorService.</summary>
        public ViewPlan RevitView { get; }

        public PlanViewRow(ViewPlan revitView, string viewType, string level)
        {
            RevitView = revitView;
            ViewName = revitView.Name;
            ViewType = viewType;
            Level = level;
        }
    }
}
