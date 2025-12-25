using System;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Creaser_V100.Models;
using Revit26_Plugin.Creaser_V100.Services;

namespace Revit26_Plugin.Creaser_V100.ViewModels
{
    public partial class RoofCreaserViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly RoofBase _selectedRoof;
        private readonly ILogService _log;

        public ObservableCollection<LogEntry> LogEntries { get; }
        public ObservableCollection<DetailFamilyOption> DetailFamilies { get; }

        private DetailFamilyOption _selectedFamily;
        public DetailFamilyOption SelectedFamily
        {
            get => _selectedFamily;
            set => SetProperty(ref _selectedFamily, value);
        }

        public IRelayCommand RunCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public event Action RequestClose;

        // ------------------------------------------------------------
        // UPDATED CONSTRUCTOR
        // ------------------------------------------------------------
        public RoofCreaserViewModel(
            UIApplication uiApp,
            RoofBase selectedRoof)
        {
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _selectedRoof = selectedRoof
                ?? throw new ArgumentNullException(nameof(selectedRoof));

            LogEntries = new ObservableCollection<LogEntry>();
            DetailFamilies = new ObservableCollection<DetailFamilyOption>();

            _log = new UiLogService(LogEntries);

            RunCommand = new RelayCommand(OnRun, CanRun);
            CloseCommand = new RelayCommand(OnClose);

            using (_log.Scope(nameof(RoofCreaserViewModel), "Constructor"))
            {
                _log.Info(nameof(RoofCreaserViewModel),
                    $"Initialized with Roof Id={_selectedRoof.Id.Value}");

                LoadDetailFamilies();
            }
        }

        private bool CanRun() => SelectedFamily != null;

        private void OnRun()
        {
            using (_log.Scope(nameof(RoofCreaserViewModel), "RunCommand"))
            {
                if (SelectedFamily == null)
                {
                    _log.Error(nameof(RoofCreaserViewModel),
                        "No detail family selected.");
                    return;
                }

                var coordinator =
                    new RoofCreaserCoordinator(
                        _uiApp,
                        _log,
                        SelectedFamily,
                        _selectedRoof);

                coordinator.Execute();
            }
        }

        private void OnClose()
        {
            RequestClose?.Invoke();
        }

        private void LoadDetailFamilies()
        {
            var context =
                new RevitContextService(_uiApp, _log);

            if (!context.Validate())
                return;

            var service =
                new DetailFamilyService(
                    context.Document,
                    _log);

            DetailFamilies.Clear();
            foreach (var family in service.GetLineBasedDetailFamilies())
                DetailFamilies.Add(family);

            if (DetailFamilies.Count > 0)
                SelectedFamily = DetailFamilies[0];
        }
    }
}
