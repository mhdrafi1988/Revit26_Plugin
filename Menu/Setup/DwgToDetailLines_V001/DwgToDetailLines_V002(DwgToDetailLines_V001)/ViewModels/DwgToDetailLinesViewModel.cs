using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media;
using Revit26_Plugin.DwgToDetailLines.V002.Helpers;
using Revit26_Plugin.DwgToDetailLines.V002.Models;
using Revit26_Plugin.DwgToDetailLines.V002.Services;

namespace Revit26_Plugin.DwgToDetailLines.V002.ViewModels
{
    public partial class DwgToDetailLinesViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly ExternalEvent _convertEvent;
        private readonly ConvertExternalEventHandler _convertHandler;

        public ObservableCollection<CadImportItem> AvailableCads { get; } = new();
        public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();

        [ObservableProperty] private CadImportItem selectedCad;
        [ObservableProperty] private SplineHandlingMode splineHandlingMode;
        [ObservableProperty] private string contextBannerText;
        [ObservableProperty] private ConversionMetrics metrics = ConversionMetrics.Empty;
        [ObservableProperty] private bool isRunning;

        public RelayCommand ConvertCommand { get; }

        public DwgToDetailLinesViewModel(UIApplication app)
        {
            _uiApp = app;

            // Must be created while on the main Revit API thread (here, during
            // window construction inside LaunchCommand.Execute's valid API context).
            _convertHandler = new ConvertExternalEventHandler();
            _convertEvent = ExternalEvent.Create(_convertHandler);

            ConvertCommand = new RelayCommand(Convert, () => SelectedCad != null && !IsRunning);

            var activeView = _uiApp.ActiveUIDocument.ActiveView;
            ContextBannerText =
                $"Context: Drafting View \"{activeView.Name}\"\n" +
                "Detail Lines will be created in the active view.";

            LoadCadImports();
        }

        private void LoadCadImports()
        {
            var items = new CadImportCollectorService(_uiApp).GetAllCadImports();
            foreach (var item in items)
                AvailableCads.Add(item);
        }

        partial void OnSelectedCadChanged(CadImportItem value)
        {
            ConvertCommand.NotifyCanExecuteChanged();

            if (value == null)
            {
                Metrics = ConversionMetrics.Empty;
                return;
            }

            // Pre-scan for the Metrics Card: Layers Found / Entities, before running.
            var activeView = _uiApp.ActiveUIDocument.ActiveView;
            var (entityCount, layerCount) = CadGeometryExtractor.PreScan(
                value.ImportInstance, _uiApp.ActiveUIDocument.Document, activeView);

            Metrics = new ConversionMetrics
            {
                LayersFound = layerCount,
                Entities = entityCount,
                Placed = null,
                Skipped = null
            };
        }

        private void AppendLog(string message, Brush color)
        {
            LogEntries.Add(new LogEntryViewModel(message, color));
        }

        private void Convert()
        {
            IsRunning = true;
            ConvertCommand.NotifyCanExecuteChanged();

            var cad = SelectedCad.ImportInstance;
            var spline = SplineHandlingMode;
            int entityCount = Metrics.Entities;
            int layerCount = Metrics.LayersFound;

            _convertHandler.Raise(_convertEvent, uiApp =>
            {
                try
                {
                    var service = new DetailLineConversionService(uiApp, AppendLog);

                    var updatedMetrics = service.Execute(
                        cad, spline, entityCount, layerCount);

                    Metrics = updatedMetrics;
                }
                catch (System.Exception ex)
                {
                    AppendLog($"[ERROR] {ex.Message}", Brushes.Red);
                    TaskDialog.Show("DWG to Detail Lines", $"Conversion failed:\n{ex.Message}");
                }
                finally
                {
                    IsRunning = false;
                    ConvertCommand.NotifyCanExecuteChanged();
                }
            });
        }
    }
}
