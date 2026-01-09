using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
//using Revit22_Plugin.SectionPlacement;
using Revit26_Plugin.SectionManager_V07.Services;
using Revit26_Plugin.SectionPlacement_V07.Models;
using Revit26_Plugin.SectionPlacement_V07.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.SectionPlacement_V07.ViewModels
{
    public partial class SectionPlacementViewModel : ObservableObject
    {
        private readonly SheetPlacementService _placementService;
        private readonly UIDocument _uidoc;

        public ObservableCollection<SectionItem> Sections { get; }
        public ObservableCollection<SectionItem> SelectedSections { get; } = new();

        public ObservableCollection<TitleBlockItem> TitleBlocks { get; }

        [ObservableProperty] private TitleBlockItem selectedTitleBlock;
        [ObservableProperty] private int rows = 6;
        [ObservableProperty] private int columns = 3;
        [ObservableProperty] private double horizontalGapMm = 300;
        [ObservableProperty] private double verticalGapMm = 50;

        public SectionPlacementViewModel(
            SectionCollectorService sectionService,
            TitleBlockService titleBlockService,
            SheetPlacementService placementService,
            UIDocument uidoc)
        {
            _placementService = placementService;
            _uidoc = uidoc;

            Sections = new ObservableCollection<SectionItem>(
                sectionService.GetUnplacedSections());

            TitleBlocks = new ObservableCollection<TitleBlockItem>(
                titleBlockService.GetTitleBlocks());

            SelectedTitleBlock = TitleBlocks.FirstOrDefault();
        }

        [RelayCommand]
        private void Place()
        {
            if (!SelectedSections.Any() || SelectedTitleBlock == null)
                return;

            _placementService.PlaceSections(
                SelectedSections.ToList(),
                SelectedTitleBlock,
                Rows,
                Columns,
                HorizontalGapMm,
                VerticalGapMm,
                _uidoc);
        }

        [RelayCommand]
        private void Cancel()
        {
            // Window closes via command binding
        }
    }
}
