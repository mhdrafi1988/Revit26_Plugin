using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.APUS_V312.ViewModels
{
    public partial class SectionItemViewModel : ObservableObject
    {
        public ViewSection View { get; }

        // ? UI-facing explicit name
        public string ViewName => View.Name;

        public int Scale => View.Scale;

        public bool IsPlaced { get; }
        public string SheetNumber { get; }
        public string PlacementScope { get; }

        [ObservableProperty]
        private bool isSelected = true;

        public SectionItemViewModel(
            ViewSection view,
            bool isPlaced,
            string sheetNumber,
            string placementScope)
        {
            View = view;
            IsPlaced = isPlaced;
            SheetNumber = sheetNumber;
            PlacementScope = placementScope;
        }
    }
}
