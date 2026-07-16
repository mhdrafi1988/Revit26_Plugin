using System.Collections.Generic;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    /// <summary>One row in the Project Views grid.</summary>
    public partial class ViewInfo : ObservableObject
    {
        public ElementId ViewId { get; init; }
        public string ViewName { get; init; } = string.Empty;
        public string ViewType { get; init; } = string.Empty;
        public string Scale { get; init; } = "—";
        public string Discipline { get; init; } = string.Empty;

        /// <summary>True when the view already sits on a sheet.</summary>
        public bool IsPlaced { get; init; }

        /// <summary>Sheet number the view is currently placed on (empty when unplaced).</summary>
        public string PlacedSheet { get; init; } = string.Empty;

        /// <summary>Column display: sheet number when placed, otherwise "No".</summary>
        public string PlacedDisplay => IsPlaced ? PlacedSheet : "No";

        /// <summary>Viewport currently hosting this view, if any (used when moving).</summary>
        public ElementId ExistingViewportId { get; init; } = ElementId.InvalidElementId;

        /// <summary>Custom parameter values keyed by definition name, for the parameter filter.</summary>
        public Dictionary<string, string> ParamValues { get; init; } = new();

        [ObservableProperty]
        private bool _isSelected;
    }
}
