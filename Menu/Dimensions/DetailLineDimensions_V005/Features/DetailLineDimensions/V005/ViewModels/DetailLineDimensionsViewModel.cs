using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Revit26_Plugin.DetailLIneDimensions.V005.Models;
using Revit26_Plugin.DetailLIneDimensions.V005.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLIneDimensions.V005.ViewModels
{
    public partial class DetailLineDimensionsViewModel : ObservableObject
    {
        private const string Version = "V005";

        private readonly Document _doc;
        private readonly View _view;
        private readonly Dispatcher _dispatcher;
        private readonly ExternalEvent _externalEvent;
        private readonly GenerateDimensionsEventHandler _handler;

        public string WindowTitle => $"Detail Line Dimensions — {Version}";
        public string VersionBadge => Version;

        public ObservableCollection<ComboItem> DetailItemTypes { get; } = new();
        public ObservableCollection<ComboItem> DimensionTypes { get; } = new();
        public ObservableCollection<LogEntry> Log { get; } = new();

        [ObservableProperty] private ComboItem selectedDetailItemType;
        [ObservableProperty] private ComboItem selectedDimensionType;

        [ObservableProperty] private int placedCount;
        [ObservableProperty] private int skippedCount;
        [ObservableProperty] private int failedCount;

        [ObservableProperty] private bool isBusy;

        public DetailLineDimensionsViewModel(UIApplication uiApp)
        {
            _doc = uiApp.ActiveUIDocument.Document;
            _view = uiApp.ActiveUIDocument.ActiveView;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _handler = new GenerateDimensionsEventHandler { OnCompleted = HandleResult };
            _externalEvent = ExternalEvent.Create(_handler);

            Initialize();
        }

        private void Initialize()
        {
            Log.Insert(0, new LogEntry(LogLevel.Info, "Initializing..."));

            DetailItemCollectorService.PopulateDetailItemTypes(_doc, _view, DetailItemTypes, Log);
            DimensionTypeService.PopulateAlignedDimensionTypes(_doc, DimensionTypes);
        }

        private bool CanGenerate =>
            !IsBusy &&
            SelectedDetailItemType != null &&
            SelectedDimensionType != null;

        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private void GenerateDimensions()
        {
            IsBusy = true;
            GenerateDimensionsCommand.NotifyCanExecuteChanged();

            _handler.DetailType = SelectedDetailItemType;
            _handler.DimensionType = SelectedDimensionType;

            _externalEvent.Raise();
        }

        private void HandleResult(DimensionResult result)
        {
            _dispatcher.Invoke(() =>
            {
                PlacedCount = result.Placed;
                SkippedCount = result.Skipped;
                FailedCount = result.Failed;

                foreach (var entry in result.Entries)
                    Log.Insert(0, entry);

                IsBusy = false;
                GenerateDimensionsCommand.NotifyCanExecuteChanged();
            });
        }

        partial void OnSelectedDetailItemTypeChanged(ComboItem value) =>
            GenerateDimensionsCommand.NotifyCanExecuteChanged();

        partial void OnSelectedDimensionTypeChanged(ComboItem value) =>
            GenerateDimensionsCommand.NotifyCanExecuteChanged();

        [RelayCommand]
        private void CopyAllLogs()
        {
            var text = string.Join(System.Environment.NewLine, Log);
            if (!string.IsNullOrEmpty(text))
                System.Windows.Clipboard.SetText(text);
        }

        [RelayCommand]
        private void ClearLogs() => Log.Clear();
    }
}
