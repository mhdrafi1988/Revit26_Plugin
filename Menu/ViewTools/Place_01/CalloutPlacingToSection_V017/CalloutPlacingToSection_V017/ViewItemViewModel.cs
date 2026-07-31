using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP.V017.ViewModels
{
    public partial class ViewItemViewModel : ObservableObject
    {
        public View View { get; }
        public string Name { get; }
        public ViewType ViewType { get; }
        public bool IsPlaced { get; }
        public string SheetNumbers { get; }

        /// <summary>
        /// Raw sheet numbers this view is placed on (unjoined), used for
        /// OR-match sheet-filter logic. Empty list if unplaced.
        /// </summary>
        public IReadOnlyList<string> SheetNumberList { get; }

        [ObservableProperty]
        private bool _isSelected;

        // Per-row reference drafting view assignments.
        // Blank unless set directly on this row, via bulk-fill, or via the
        // session's last-used per-slot default (applied once at construction
        // for rows that don't already have a real per-view assignment).
        [ObservableProperty]
        private DraftingViewItemViewModel _leftView;

        [ObservableProperty]
        private DraftingViewItemViewModel _centerView;

        [ObservableProperty]
        private DraftingViewItemViewModel _rightView;

        public ViewItemViewModel(View view, IReadOnlyList<string> sheetNumbers)
        {
            View = view;
            Name = view.Name;
            ViewType = view.ViewType;

            SheetNumberList = sheetNumbers ?? new List<string>();
            IsPlaced = SheetNumberList.Any();
            SheetNumbers = IsPlaced
                ? string.Join(", ", SheetNumberList)
                : string.Empty;
        }
    }
}
