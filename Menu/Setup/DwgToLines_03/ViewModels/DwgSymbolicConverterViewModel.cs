using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;
using Revit26_Plugin.DwgSymbolicConverter_V03.Services;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.ViewModels
{
    public partial class DwgSymbolicConverterViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;

        public ObservableCollection<CadImportItem> AvailableCads { get; } = new();

        [ObservableProperty] private CadImportItem selectedCad;
        [ObservableProperty] private PlacementMode placementMode;
        [ObservableProperty] private SplineHandlingMode splineHandlingMode;
        [ObservableProperty] private bool extrusionReadyMode;

        public RelayCommand ConvertCommand { get; }

        public DwgSymbolicConverterViewModel(UIApplication app)
        {
            _uiApp = app;
            ConvertCommand = new RelayCommand(Convert, () => SelectedCad != null);
        }

        partial void OnSelectedCadChanged(CadImportItem v)
            => ConvertCommand.NotifyCanExecuteChanged();

        private void Convert()
        {
            new CadConversionService(_uiApp,
                (m, c) => { })
                .Execute(
                    SelectedCad.ImportInstance,
                    PlacementMode,
                    SplineHandlingMode,
                    ExtrusionReadyMode);
        }
    }
}
