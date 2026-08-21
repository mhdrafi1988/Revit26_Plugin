using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SectionViewAutoTagger.V004
{
    /// <summary>
    /// One row in the "Section Views on Sheet" multi-select popover.
    /// IsSelected is two-way bound to a checkbox; ObservableObject so
    /// toggling refreshes dependent UI (e.g. enabling "Add to Worklist").
    /// </summary>
    public partial class SectionViewOption : ObservableObject
    {
        /// <summary>ElementId of the underlying ViewSection.</summary>
        public ElementId ViewId { get; }

        /// <summary>View.Name, e.g. "Section 1 - Corridor".</summary>
        public string ViewName { get; }

        [ObservableProperty]
        private bool isSelected;

        public SectionViewOption(ElementId viewId, string viewName, bool isSelected = false)
        {
            ViewId = viewId;
            ViewName = viewName;
            this.isSelected = isSelected;
        }
    }
}
