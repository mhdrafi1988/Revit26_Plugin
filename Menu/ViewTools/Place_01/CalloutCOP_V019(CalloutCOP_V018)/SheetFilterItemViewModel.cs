using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.CalloutCOP.V019.ViewModels
{
    /// <summary>
    /// Single checkbox row in the sheet-filter popup checklist.
    /// The "ALL" pseudo-entry is represented as IsAllRow=true with SheetNumber
    /// left blank; it is mutually exclusive with every specific-sheet row -
    /// enforced in CalloutCOPViewModel, not here.
    /// </summary>
    public partial class SheetFilterItemViewModel : ObservableObject
    {
        public string SheetNumber { get; }
        public bool IsAllRow { get; }

        /// <summary>Text shown in the checklist row - "ALL (no filter)" for the pinned ALL row, else the sheet number.</summary>
        public string DisplayText { get; }

        [ObservableProperty]
        private bool _isChecked;

        public SheetFilterItemViewModel(string sheetNumber, bool isAllRow = false)
        {
            SheetNumber = sheetNumber;
            IsAllRow = isAllRow;
            DisplayText = isAllRow ? "ALL (no filter)" : sheetNumber;
        }
    }
}
