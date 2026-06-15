using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP_V06.ViewModels
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
