using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.UI;
using Revit26_Plugin.Creaser_V31.Services;

namespace Revit26_Plugin.Creaser_V31.ViewModels
{
    public partial class CreaserViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly DrainArrowOrchestrator _orchestrator;

        [ObservableProperty]
        private string statusMessage = "Ready.";

        public CreaserViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            _orchestrator = new DrainArrowOrchestrator();
        }

        [RelayCommand]
        private void Run()
        {
            StatusMessage = "Placing roof drain slope arrows…";

            // SAFE: already on Revit API thread
            _orchestrator.Execute(_uiDoc);

            StatusMessage = "Completed successfully.";
        }
    }
}
