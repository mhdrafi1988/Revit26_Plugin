using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.APUS_V313.ViewModels
{
    public partial class SectionItemViewModel : ObservableObject
    {
        public ViewSection View { get; }

        // UI-facing explicit name
        public string ViewName => View.Name;
        public ElementId ViewId => View?.Id ?? ElementId.InvalidElementId;

        // Paper-space dimensions (in feet) - calculated on demand
        public double ViewWidth
        {
            get
            {
                if (View == null) return 0;
                var fp = Services.ViewSizeService.Calculate(View);
                return fp.WidthFt;
            }
        }

        public double ViewHeight
        {
            get
            {
                if (View == null) return 0;
                var fp = Services.ViewSizeService.Calculate(View);
                return fp.HeightFt;
            }
        }

        public int Scale => View?.Scale ?? 1;

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