using System;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreaserAdv_V002.Services;
using Revit26_Plugin.CreaserAdv_V002.Services; // <-- Add this using directive

namespace Revit26_Plugin.CreaserAdv_V002.ViewModels
{
    public partial class CreaserAdvViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly Element _roof;
        private readonly LoggingService _log;

        public ObservableCollection<FamilySymbol> DetailSymbols { get; }
            = new ObservableCollection<FamilySymbol>();

        public ObservableCollection<LogEntry> LogEntries
            => _log.Entries;

        private FamilySymbol _selectedDetailSymbol;
        public FamilySymbol SelectedDetailSymbol
        {
            get => _selectedDetailSymbol;
            set => SetProperty(ref _selectedDetailSymbol, value);
        }

        public CreaserAdvViewModel(
            UIApplication uiApp,
            Element roof,
            LoggingService log)
        {
            _uiDoc = uiApp?.ActiveUIDocument
                ?? throw new ArgumentNullException(nameof(uiApp));

            _doc = _uiDoc.Document;
            _roof = roof ?? throw new ArgumentNullException(nameof(roof));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            LoadDetailSymbols();
        }

        private void LoadDetailSymbols()
        {
            DetailSymbols.Clear();

            var collector =
                new DetailItemCollectorService(_doc, _log);

            foreach (var symbol in collector.Collect())
                DetailSymbols.Add(symbol);

            if (DetailSymbols.Count > 0)
                SelectedDetailSymbol = DetailSymbols[0];
        }

        [RelayCommand]
        private void Run()
        {
            if (SelectedDetailSymbol == null)
            {
                TaskDialog.Show(
                    "Creaser Advanced",
                    "Select a detail item.");
                return;
            }

            if (_doc.ActiveView is not ViewPlan planView)
            {
                TaskDialog.Show(
                    "Creaser Advanced",
                    "Run in a Plan View.");
                return;
            }

            _log.Info("Run started.");

            using (var tx =
                   new Transaction(_doc, "Creaser Advanced"))
            {
                tx.Start();

                var pipeline =
                    new RoofGeometryPipelineService(_log);

                PipelineResult result =
                    pipeline.Execute(_uiDoc, _roof);

                var orchestrator =
                    new DetailItemPlacementOrchestrator();

                orchestrator.Execute(
                    _doc,
                    planView,
                    result,
                    SelectedDetailSymbol,
                    _log);

                tx.Commit();
            }

            _log.Info("Run completed.");
        }

        [RelayCommand]
        private void ClearLog()
        {
            _log.Clear();
        }
    }
}
