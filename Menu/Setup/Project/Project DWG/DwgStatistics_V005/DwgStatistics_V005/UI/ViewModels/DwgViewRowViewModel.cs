using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.DwgStatistics.V005.Core.Models;

namespace Revit26_Plugin.DwgStatistics.V005.UI.ViewModels
{
    /// <summary>
    /// One row of the Views tab (V002): a view that owns, draws or (V004) hides the selected DWG, plus the
    /// tick used by Open / Hide / Unhide / Delete in Ticked Views.
    /// </summary>
    public partial class DwgViewRowViewModel : ObservableObject
    {
        /// <summary>Creates a row for <paramref name="usage"/>.</summary>
        public DwgViewRowViewModel(DwgViewUsage usage)
        {
            Usage = usage ?? throw new ArgumentNullException(nameof(usage));
        }

        /// <summary>The view usage behind the row (also the Open button's command parameter).</summary>
        public DwgViewUsage Usage { get; }

        /// <summary>Ticked for Open Ticked Views.</summary>
        [ObservableProperty] private bool isTicked;

        /// <summary>Element ID of the view.</summary>
        public long ViewId => Usage.ViewId;

        /// <summary>View name.</summary>
        public string ViewName => Usage.ViewName;

        /// <summary>View type label.</summary>
        public string ViewType => Usage.ViewType;

        /// <summary>"Placed here" / "Visible (model)" / "Visible (some hidden)" / "Hidden here".</summary>
        public string Relation => Usage.Relation;

        /// <summary>V004: instances hidden by element in the view.</summary>
        public int HiddenInstances => Usage.HiddenInstances;

        /// <summary>V004: true when the file is only hidden here (not drawn) — row shown muted.</summary>
        public bool IsHiddenOnly => !Usage.IsPlaced && Usage.VisibleInstances == 0;

        /// <summary>V004: true for 3D views.</summary>
        public bool Is3D => Usage.Is3D;

        /// <summary>V005: layers of the file hidden in this view.</summary>
        public int HiddenLayers => Usage.HiddenLayers;

        /// <summary>V005: layers of the file overridden in this view.</summary>
        public int OverriddenLayers => Usage.OverriddenLayers;

        /// <summary>Instances owned by the view.</summary>
        public int PlacedInstances => Usage.PlacedInstances;

        /// <summary>Instances drawn in the view.</summary>
        public int VisibleInstances => Usage.VisibleInstances;

        /// <summary>Sheet numbers.</summary>
        public string Sheets => Usage.Sheets;
    }
}
