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
            if (!_vm.ValidateInputs(out string err))
            {
                _log.Error(err);
                return;
            }

            var options = new SectionCreationOptions(
                _vm.SectionPrefix,
                _vm.FarClipMm,
                _vm.TopPaddingMm,
                _vm.BottomPaddingMm,
                _vm.BottomOffsetMm,
                _vm.SelectedSnapSource,
                _vm.SelectedSectionType,
                _vm.SelectedTemplate,
                _vm.OpenAllAfterCreate,
                _vm.DeleteLinesAfterCreate);

            var orientSvc = new SectionOrientationService();
            var hostSvc = new HostElementSearchService(_doc);
            var createSvc = new SectionCreationService(_doc, _plan);
            var cleanup = new PostCreationCleanupService(_doc);

            var created = new List<ElementId>();
            UIDocument uiDoc = new UIDocument(_doc);

            using TransactionGroup tg =
                new(_doc, "Create Sections From Lines");
            tg.Start();

            foreach (var r in refs)
            {
                if (_vm.Execution.Token.IsCancellationRequested)
                {
                    _log.Warn("Cancelled by user.");
                    break;
                }

                if (_doc.GetElement(r) is not DetailLine dl ||
                    dl.GeometryCurve is not Line line)
                    continue;

                _log.Info($"Processing line {dl.Id.Value}");

                var orient = orientSvc.Calculate(line);
                var host = hostSvc
                    .FindCandidates(
                        orient.MidPoint,
                        _vm.SearchThresholdMm,
                        options.SnapSource)
                    .FirstOrDefault();

                if (host == null)
                {
                    _log.Warn("No host found.");
                    continue;
                }

                using Transaction tx =
                    new(_doc, "Create Section");
                tx.Start();

                var req = new SectionCreationRequest(
                    dl, line, orient, host, options);

                var result = createSvc.Create(req, out bool renamed);

                if (!result.Success)
                {
                    _log.Error(result.ErrorMessage);
                    tx.RollBack();
                    continue;
                }

                if (renamed)
                    _log.Warn($"Renamed to {result.Section.Name}");

                created.Add(dl.Id);
                _log.Info($"Created {result.Section.Name}");

                tx.Commit();

                if (options.OpenAfterCreate)
                    uiDoc.ActiveView = result.Section;
            }

            tg.Assimilate();

            cleanup.DeleteDetailLines(
                created,
                options.DeleteLinesAfterCreate);

            _log.Info("Completed.");
        }
    }
}
