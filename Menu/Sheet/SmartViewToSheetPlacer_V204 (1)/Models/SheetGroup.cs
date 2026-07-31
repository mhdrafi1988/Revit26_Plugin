using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.Models
{
    /// <summary>
    /// Represents one suggested (and later, actually created) sheet: its
    /// auto-generated number/name and the list of ViewPlacement rows packed
    /// onto it. Sheets are always grouped by RevitViewType (never mixed) —
    /// see <see cref="ViewInfo.RevitViewType"/>.
    /// </summary>
    public partial class SheetGroup : ObservableObject
    {
        /// <summary>1-based suggested sheet index within its ViewType group (used for display + name recompute).</summary>
        [ObservableProperty]
        private int _sheetIndex;

        /// <summary>The ViewType this sheet's views all share (non-mixed placement).</summary>
        public ViewType RevitViewType { get; }

        /// <summary>Views placed on this sheet.</summary>
        public ObservableCollection<ViewPlacement> Placements { get; } = new();

        /// <summary>
        /// Auto-generated sheet name: "Type (Count)" for a single type, e.g. "Floor Plans (3)".
        /// Since a SheetGroup only ever holds one ViewType (non-mixed), this is
        /// always a single Type (Count) entry — never a comma-joined mixed list.
        /// </summary>
        public string GeneratedName => $"{Services.ViewTypeLabelHelper.Pluralize(RevitViewType)} ({Placements.Count})";

        /// <summary>Sheet number assigned after creation (set in Stage 3/4). Null until placed.</summary>
        [ObservableProperty]
        private string? _assignedSheetNumber;

        /// <summary>ElementId of the created ViewSheet. Null until placed.</summary>
        [ObservableProperty]
        private ElementId? _createdSheetId;

        /// <summary>Display text for Stage 4's Status column: "Created" once
        /// CreatedSheetId is set, "Pending" beforehand.</summary>
        public string StatusText => CreatedSheetId != null ? "Created" : "Pending";

        partial void OnCreatedSheetIdChanged(ElementId? value) => OnPropertyChanged(nameof(StatusText));

        /// <summary>Whether this sheet should be opened in Revit after placement completes (Stage 4 checkbox).</summary>
        [ObservableProperty]
        private bool _openAfterPlacement = true;

        public SheetGroup(ViewType revitViewType, int sheetIndex)
        {
            RevitViewType = revitViewType;
            _sheetIndex = sheetIndex;

            // GeneratedName ("Type (Count)") and TotalViewCount both depend on
            // Placements.Count, but ObservableCollection does not automatically
            // notify computed properties that read it — raise both properties'
            // change notifications whenever the collection changes (add/remove/
            // move), so the DataGrid/badge text stays in sync after Stage 2's
            // manual sheet-reassignment moves a placement between sheets.
            Placements.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(GeneratedName));
                OnPropertyChanged(nameof(TotalViewCount));
            };
        }

        public int TotalViewCount => Placements.Count;
    }

    /// <summary>
    /// A single view's placement assignment within a SheetGroup: which sheet
    /// it's on, where on the sheet, and whether it currently fits.
    /// </summary>
    public partial class ViewPlacement : ObservableObject
    {
        public ViewInfo View { get; }

        /// <summary>The SheetGroup this view is currently assigned to. Settable so Stage 2's
        /// manual override (dropdown) can re-parent a placement to a different sheet
        /// within the same ViewType group.</summary>
        [ObservableProperty]
        private SheetGroup _assignedSheet;

        /// <summary>
        /// All sheets this placement's dropdown may offer — restricted to
        /// sheets sharing the same RevitViewType as this view (per confirmed
        /// non-mixed rule: a placement can only move to a sheet within its
        /// own ViewType group, never to a different group's sheet). Set once
        /// by the ViewModel/packing service after all SheetGroups for this
        /// view's group are known.
        /// </summary>
        public ObservableCollection<SheetGroup> AvailableSheets { get; } = new();

        /// <summary>Position label on the sheet, e.g. "Top-Left", "Bottom-Right".</summary>
        [ObservableProperty]
        private string _position = string.Empty;

        /// <summary>X offset in mm from the sheet's usable-area origin.</summary>
        [ObservableProperty]
        private double _offsetXMm;

        /// <summary>Y offset in mm from the sheet's usable-area origin.</summary>
        [ObservableProperty]
        private double _offsetYMm;

        /// <summary>Canvas.Left in px for the Stage 2 Sheet Preview canvas — sheet-origin-relative
        /// (usable-area-left-px + OffsetXMm*scale). Recomputed by the ViewModel whenever the
        /// selected preview sheet, titleblock, or margin/gap values change.</summary>
        [ObservableProperty]
        private double _previewLeftPx;

        /// <summary>Canvas.Top in px for the Stage 2 Sheet Preview canvas — sheet-origin-relative
        /// (usable-area-top-px + OffsetYMm*scale).</summary>
        [ObservableProperty]
        private double _previewTopPx;

        /// <summary>View box width in px for the Stage 2 Sheet Preview canvas (View.WidthMm * scale).</summary>
        [ObservableProperty]
        private double _previewWidthPx;

        /// <summary>View box height in px for the Stage 2 Sheet Preview canvas (View.HeightMm * scale).</summary>
        [ObservableProperty]
        private double _previewHeightPx;

        /// <summary>Whether this placement currently fits within its assigned sheet's usable area.</summary>
        [ObservableProperty]
        private bool _fits = true;

        public ViewPlacement(ViewInfo view, SheetGroup assignedSheet)
        {
            View = view;
            _assignedSheet = assignedSheet;
        }
    }
}
