using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin_Revised.DwgStatistics.V005.Core.Models;

namespace Revit26_Plugin_Revised.DwgStatistics.V005.UI.ViewModels
{
    /// <summary>
    /// V005: one row of the Layers tab — a DWG layer with how many views hide / override it, plus the tick
    /// used by Hide / Unhide Ticked Layers in Ticked Views.
    /// </summary>
    public partial class DwgLayerRowViewModel : ObservableObject
    {
        /// <summary>Creates a row for <paramref name="layer"/>.</summary>
        public DwgLayerRowViewModel(DwgLayerStats layer)
        {
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));
        }

        /// <summary>The layer statistics behind the row.</summary>
        public DwgLayerStats Layer { get; }

        /// <summary>Ticked for the layer actions.</summary>
        [ObservableProperty] private bool isTicked;

        /// <summary>Sub-category element ID.</summary>
        public long LayerId => Layer.LayerId;

        /// <summary>Layer name.</summary>
        public string Name => Layer.Name;

        /// <summary>Views hiding the layer.</summary>
        public int HiddenInViews => Layer.HiddenInViews;

        /// <summary>Views overriding the layer.</summary>
        public int OverriddenInViews => Layer.OverriddenInViews;

        /// <summary>Names of the views hiding it.</summary>
        public string HiddenViewNames => Layer.HiddenViewNames;
    }
}
