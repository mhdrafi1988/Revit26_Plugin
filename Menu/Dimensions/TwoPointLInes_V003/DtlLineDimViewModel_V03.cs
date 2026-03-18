using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using Revit26_Plugin.DtlLineDim_V03.Models;
using Revit26_Plugin.DtlLineDim_V03.Services;

namespace Revit26_Plugin.DtlLineDim_V03.ViewModels
{
    public partial class DtlLineDimViewModel_V03 : ObservableObject
    {
        private readonly Document _doc;
        private readonly View _view;

        public ObservableCollection<ComboItem> DetailItemTypes { get; } = new();
        public ObservableCollection<ComboItem> DimensionTypes { get; } = new();
        public ObservableCollection<string> StatusLog { get; } = new();

        public string StatusLogText => string.Join(Environment.NewLine, StatusLog);

        [ObservableProperty] private ComboItem selectedDetailItemType;
        [ObservableProperty] private ComboItem selectedDimensionType;

        public DtlLineDimViewModel_V03(UIApplication uiApp)
        {
            _doc = uiApp.ActiveUIDocument.Document;
            _view = _doc.ActiveView;

            StatusLog.CollectionChanged += (_, __) =>
                OnPropertyChanged(nameof(StatusLogText));

            Initialize();
        }

        private void Initialize()
        {
            StatusLog.Clear();
            StatusLog.Add("Initializing...");

            DetailItemCollectorService.PopulateDetailItemTypes(
                _doc, _view, DetailItemTypes, StatusLog);

            DimensionTypeService.PopulateAlignedDimensionTypes(
                _doc, DimensionTypes);
        }

        private bool CanGenerate =>
            SelectedDetailItemType != null &&
            SelectedDimensionType != null;

        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private void GenerateDimensions()
        {
            DimensionCreationService.CreateDimensions(
                _doc, _view,
                SelectedDetailItemType,
                SelectedDimensionType,
                StatusLog);
        }

        partial void OnSelectedDetailItemTypeChanged(ComboItem _) =>
            GenerateDimensionsCommand.NotifyCanExecuteChanged();

        partial void OnSelectedDimensionTypeChanged(ComboItem _) =>
            GenerateDimensionsCommand.NotifyCanExecuteChanged();
    }
}
