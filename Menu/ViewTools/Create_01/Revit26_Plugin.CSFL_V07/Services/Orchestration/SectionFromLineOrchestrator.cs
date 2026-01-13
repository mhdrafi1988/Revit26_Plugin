using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.CSFL_V07.Models;
using Revit26_Plugin.CSFL_V07.Services.Geometry;
using Revit26_Plugin.CSFL_V07.Services.Search;
using Revit26_Plugin.CSFL_V07.Services.Creation;
using Revit26_Plugin.CSFL_V07.Services.Cleanup;
using Revit26_Plugin.CSFL_V07.Services.Logging;
using Revit26_Plugin.CSFL_V07.ViewModels;

namespace Revit26_Plugin.CSFL_V07.Services.Orchestration
{
    /// <summary>
    /// Orchestrates the full workflow of creating sections from detail lines.
    /// Owns all Revit transactions.
    /// </summary>
    public class SectionFromLineOrchestrator
    {
        private readonly Document _doc;
        private readonly ViewPlan _plan;
        private readonly SectionFromLineViewModel _vm;
        private readonly LiveLogService _log;

        public SectionFromLineOrchestrator(
            Document doc,
            ViewPlan plan,
            SectionFromLineViewModel vm)
        {
            _doc = doc;
            _plan = plan;
            _vm = vm;
            _log = new LiveLogService(vm.LiveLog);
        }

        public void Start(IList<Reference> refs)
        {
            // ---------------- VALIDATION ----------------
            if (!_vm.ValidateInputs(out string error))
            {
                _log.Error(error);
                return;
            }

            var orientationSvc = new SectionOrientationService();
            var hostSvc = new HostElementSearchService(_doc);
            var createSvc = new SectionCreationService(_doc, _plan);
            var cleanupSvc = new PostCreationCleanupService(_doc);

            var createdLineIds = new List<ElementId>();
            UIDocument uiDoc = new UIDocument(_doc);

            // ---------------- TRANSACTIONS ----------------
            using TransactionGroup tg =
                new TransactionGroup(_doc, "Create Sections From Detail Lines");
            tg.Start();

            using Transaction tx =
                new Transaction(_doc, "Create Sections");
            tx.Start();

            foreach (var r in refs)
            {
                if (_vm.Execution.Token.IsCancellationRequested)
                {
                    _log.Warn("Operation cancelled by user.");
                    break;
                }

                if (_doc.GetElement(r) is not DetailLine dl)
                    continue;

                if (dl.GeometryCurve is not Line line)
                    continue;

                _log.Info($"Processing line {dl.Id.Value}");

                var orientation = orientationSvc.Calculate(line);
                if (!orientation.Success)
                {
                    _log.Warn("Failed to calculate orientation.");
                    continue;
                }

                var host = hostSvc
                    .FindCandidates(
                        orientation.MidPoint,
                        _vm.SearchThresholdMm,
                        _vm.SelectedSnapSource)
                    .FirstOrDefault();

                if (host == null)
                {
                    _log.Warn($"No host element found for line {dl.Id.Value}");
                    continue;
                }

                var request = new SectionCreationRequest(
                    dl,
                    line,
                    orientation,
                    host,
                    _vm);

                var result = createSvc.Create(request,out bool nameRenamed);


                if (!result.Success)
                {
                    _log.Error(result.ErrorMessage);
                    continue;
                }

                if (nameRenamed)
                    _log.Warn($"Section name existed ? renamed to {result.Section.Name}");

                createdLineIds.Add(dl.Id);
                _log.Info($"Created section: {result.Section.Name}");

                if (_vm.OpenAllAfterCreate)
                    uiDoc.ActiveView = result.Section;
            }

            // ---------------- COMMIT ----------------
            tx.Commit();
            tg.Assimilate();

            cleanupSvc.DeleteDetailLines(
                createdLineIds,
                _vm.DeleteLinesAfterCreate);

            _log.Info("Section creation completed.");
        }
    }
}
