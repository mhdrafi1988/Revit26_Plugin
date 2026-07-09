using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP.V015.ViewModels
{
    public partial class ViewItemViewModel : ObservableObject
    {
        public View View { get; }
        public string Name { get; }
        public ViewType ViewType { get; }
        public bool IsPlaced { get; }
        public string SheetNumbers { get; }

        [ObservableProperty]
        private bool _isSelected;

        // Per-row reference drafting view assignments.
        // No fallback - each slot is blank unless set directly on this row
        // (or via bulk-fill).
        [ObservableProperty]
        private ViewDrafting _leftView;

        [ObservableProperty]
        private ViewDrafting _centerView;

        [ObservableProperty]
        private ViewDrafting _rightView;

        public ViewItemViewModel(View view, IReadOnlyList<string> sheetNumbers)
        {
            View = view;
            Name = view.Name;
            ViewType = view.ViewType;

            IsPlaced = sheetNumbers != null && sheetNumbers.Any();
            SheetNumbers = IsPlaced
                ? string.Join(", ", sheetNumbers)
                : string.Empty;
        }
    }
}
