using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V01.Helpers;
using Revit26_Plugin.DwgSymbolicConverter_V01.Models;
using Revit26_Plugin.DwgSymbolicConverter_V01.Services;

// ? ALIAS TO AVOID WPF CONFLICT
using PlacementModeModel =
    Revit26_Plugin.DwgSymbolicConverter_V01.Models.PlacementMode;

namespace Revit26_Plugin.DwgSymbolicConverter_V01.ViewModels
{
    public partial class DwgSymbolicConverterViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;

        public ObservableCollection<LogEntryViewModel> LogEntries { get; } = new();
        public ObservableCollection<CadGeometrySummary> GeometrySummary { get; } = new();

        [ObservableProperty] private string selectedCadFileName;
        [ObservableProperty] private string selectedCadElementId;
        [ObservableProperty] private string cadImportType;

        // ---------- Spline handling ----------
        [ObservableProperty] private bool preserveSplinesMode = true;
        [ObservableProperty] private bool tessellateSplinesMode;

        // ---------- Placement mode ----------
        [ObservableProperty] private bool placeSymbolic = true;
        [ObservableProperty] private bool placeModel;
        [ObservableProperty] private bool placeBoth;

        public bool CanProcess => !string.IsNullOrEmpty(SelectedCadElementId);

        public IRelayCommand ProcessCommand { get; }

        public DwgSymbolicConverterViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            ProcessCommand = new RelayCommand(Process);

            LogInfo("Family Editor detected");
            LoadCadSelection();
        }

        private void LoadCadSelection()
        {
            try
            {
                var service = new CadSelectionService(_uiApp);
                CadFileInfo info = service.GetSelectedCad();

                SelectedCadFileName = info.FileName;
                SelectedCadElementId = info.ElementId;
                CadImportType = info.ImportType;

                LogInfo($"CAD file selected: {info.FileName}");

                var scanService = new CadGeometryScanService(_uiApp, Log);
                scanService.Scan(info, GeometrySummary);
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
            }
        }

        private void Process()
        {
            SplineHandlingMode splineMode =
                PreserveSplinesMode
                    ? SplineHandlingMode.Preserve
                    : SplineHandlingMode.Tessellate;

            PlacementModeModel placementMode =
                PlaceBoth ? PlacementModeModel.SymbolicAndModel :
                PlaceModel ? PlacementModeModel.ModelOnly :
                PlacementModeModel.SymbolicOnly;

            LogInfo($"Spline mode: {splineMode}");
            LogInfo($"Placement mode: {placementMode}");

            var converter = new CadConversionService(_uiApp, Log);
            converter.Execute(splineMode, placementMode);
        }

        // ---------- Logging helpers ----------

        private void Log(string message, Brush color)
        {
            UiDispatcherHelper.Run(() =>
                LogEntries.Add(new LogEntryViewModel(message, color)));
        }

        private void LogInfo(string message)
            => Log($"[INFO] {message}", Brushes.White);

        private void LogError(string message)
            => Log($"[ERROR] {message}", Brushes.Red);
    }
}
