using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.ViewModels
{
    /// <summary>
    /// One collapsible group card in the Detected Openings list: a display key
    /// ("Circle", "Rectangle", "Other" — Square is folded into Rectangle for
    /// display and sizing purposes, confirmed with Rafi), its member openings,
    /// and its own Auto/Fixed callout sizing controls (GroupSizingViewModel).
    /// </summary>
    public partial class OpeningGroupViewModel : ObservableObject
    {
        /// <summary>Display/group key: "Circle", "Rectangle", or "Other".</summary>
        public string GroupKey { get; }

        public ObservableCollection<OpeningItem> Openings { get; } = new();

        public GroupSizingViewModel Sizing { get; }

        [ObservableProperty]
        private bool isExpanded = true;

        public int Count => Openings.Count;
        public int SelectedCount => Openings.Count(o => o.IsSelected);

        public OpeningGroupViewModel(string groupKey, GroupSizingViewModel sizing)
        {
            GroupKey = groupKey;
            Sizing = sizing;
        }

        /// <summary>Refresh Count/SelectedCount bindings after a checkbox changes.</summary>
        public void RefreshCounts()
        {
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(SelectedCount));
        }

        public void SelectAll()
        {
            foreach (var o in Openings) o.IsSelected = true;
            RefreshCounts();
        }

        public void SelectNone()
        {
            foreach (var o in Openings) o.IsSelected = false;
            RefreshCounts();
        }

        /// <summary>Maps the 4-value OpeningShape enum down to this UI's 3 group keys (Square folds into Rectangle).</summary>
        public static string KeyFor(OpeningShape shape) => shape switch
        {
            OpeningShape.Circle => "Circle",
            OpeningShape.Rectangle => "Rectangle",
            OpeningShape.Square => "Rectangle",
            _ => "Other"
        };
    }
}
