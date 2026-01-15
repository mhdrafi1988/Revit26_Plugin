using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv_V003.Models;
using Revit26_Plugin.CreaserAdv_V003.Services;
using Revit26_Plugin.DtlLineDim_V03.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Revit26_Plugin.CreaserAdv_V003.ViewModels
{
    public class CreaserAdvViewModel
    {
        private readonly UIDocument _uiDoc;
        private readonly Element _roof;
        private readonly LoggingService _log;

        public ObservableCollection<LogEntry> LogEntries => _log.Entries;

        public ICommand RunCommand { get; }

        public CreaserAdvViewModel(
            UIDocument uiDoc,
            Element roof)
        {
            _uiDoc = uiDoc;
            _roof = roof;

            _log = new LoggingService();

            RunCommand = new RelayCommand(Run);
        }

        private void Run()
        {
            var doc = _uiDoc.Document;
            var view = doc.ActiveView as ViewPlan;

            if (view == null)
            {
                _log.Info("Active view is not a plan view.");
                return;
            }

            var pipeline =
                new RoofGeometryPipelineService(_log);

            var result =
                pipeline.Execute(_uiDoc, _roof);

            var symbols =
                new DetailItemCollectorService(doc, _log)
                    .Collect();

            if (symbols.Count == 0)
            {
                _log.Info("No detail item symbols found.");
                return;
            }

            var orchestrator =
                new DetailItemPlacementOrchestrator();

            orchestrator.Execute(
                doc,
                view,
                result,
                symbols[0],
                _log);
        }
    }
}
