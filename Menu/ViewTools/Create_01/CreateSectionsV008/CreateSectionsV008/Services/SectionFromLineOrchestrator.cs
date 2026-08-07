using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.Helpers;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.Models;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.ViewModels;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.Services
{
    /// <summary>
    /// Coordinates the full per-line pipeline: orientation -> host search
    /// (with V008 threshold-fallback) -> section creation -> naming ->
    /// cleanup, updating live metrics on the ViewModel as it goes.
    ///
    /// V008 changes from V07:
    /// - Runs from IExternalEventHandler.Execute() (see SectionCreationExternalEvent),
    ///   not directly off the VM's CreateRequested event on the UI thread —
    ///   this is what makes the live log actually live and Cancel actually work.
    /// - Increments VM metric counters (Selected/Created/Skipped/Failed/Renamed)
    ///   as each line resolves, for the metric cards.
    /// - Uses HostElementSearchService's new below/passing-through Z rule and
    ///   4-category HostSourceSelection instead of SnapSourceMode.
    /// - Falls back to threshold-based height (via SectionCreationService)
    ///   when no host qualifies and UseThresholdFallback is checked, instead
    ///   of unconditionally skipping the line.
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
            if (!_vm.ValidateInputs(out string err))
            {
                _log.Error(err);
                return;
            }

            _vm.ResetMetrics();
            _vm.SelectedCount = refs.Count;
            _vm.IsRunning = true;
            _vm.Execution.Reset();

            var hostSource = new HostSourceSelection
            {
                FloorHost = _vm.SearchFloorHost,
                RoofHost = _vm.SearchRoofHost,
                FloorLinked = _vm.SearchFloorLinked,
                RoofLinked = _vm.SearchRoofLinked
            };

            var options = new SectionCreationOptions
            {
                Prefix = _vm.SectionPrefix,
                FarClipMm = _vm.FarClipMm,
                TopPaddingMm = _vm.TopPaddingMm,
                BottomPaddingMm = _vm.BottomPaddingMm,
                BottomOffsetMm = _vm.BottomOffsetMm,
                SearchThresholdMm = _vm.SearchThresholdMm,
                HostSource = hostSource,
                UseThresholdFallback = _vm.UseThresholdFallback,
                SectionType = _vm.SelectedSectionType,
                Template = _vm.SelectedTemplate,
                OpenAfterCreate = _vm.OpenAllAfterCreate,
                DeleteLinesAfterCreate = _vm.DeleteLinesAfterCreate,
                ViewScale = _vm.ViewScale
            };

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
                if (!orient.Success)
                {
                    _log.Warn($"Line {dl.Id.Value}: could not compute orientation. Skipped.");
                    _vm.SkippedCount++;
                    continue;
                }

                var candidates = hostSvc.FindCandidates(
                    orient.MidPoint,
                    orient.MidPoint.Z,
                    options.SearchThresholdMm,
                    options.HostSource);

                var host = candidates.FirstOrDefault();

                if (host == null && !options.UseThresholdFallback)
                {
                    _log.Warn($"Line {dl.Id.Value}: no qualifying host found. Skipped.");
                    _vm.SkippedCount++;
                    continue;
                }

                if (host == null)
                    _log.Warn($"Line {dl.Id.Value}: no qualifying host found — using threshold fallback ({options.SearchThresholdMm}mm).");

                using Transaction tx =
                    new(_doc, "Create Section");
                tx.Start();

                var req = new SectionCreationRequest
                {
                    SourceLine = dl,
                    GeometryLine = line,
                    Orientation = orient,
                    HostElement = host,
                    Options = options
                };

                var result = createSvc.Create(req, out bool renamed, out bool usedFallback);

                if (!result.Success)
                {
                    _log.Error($"Line {dl.Id.Value}: {result.ErrorMessage}");
                    _vm.FailedCount++;
                    tx.RollBack();
                    continue;
                }

                if (renamed)
                {
                    _log.Warn($"Renamed to {result.Section.Name}");
                    _vm.RenamedCount++;
                }

                created.Add(dl.Id);
                _vm.CreatedCount++;
                _log.Success($"Created {result.Section.Name}"
                    + (usedFallback ? " (threshold fallback)" : string.Empty));

                tx.Commit();

                if (options.OpenAfterCreate)
                    uiDoc.ActiveView = result.Section;
            }

            tg.Assimilate();

            cleanup.DeleteDetailLines(
                created,
                options.DeleteLinesAfterCreate);

            _vm.IsRunning = false;

            _log.Info(
                $"Completed. {_vm.CreatedCount} created | " +
                $"{_vm.SkippedCount} skipped | {_vm.FailedCount} failed | " +
                $"{_vm.RenamedCount} renamed");

            _vm.SaveSettings();
            ExportLog();
        }

        /// <summary>
        /// V008: new. Auto-saves the log on completion, per suite logging
        /// convention. Asks for a save folder once per session (reused via
        /// _vm.LogSaveFolder, which is also persisted to settings.json so
        /// it's remembered across sessions too — flagged as an assumption
        /// in SectionCreationSettings).
        /// </summary>
        private void ExportLog()
        {
            if (string.IsNullOrWhiteSpace(_vm.LogSaveFolder))
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Choose a folder for Create Sections From Detail Lines logs"
                };

                if (dialog.ShowDialog() != true)
                {
                    _log.Warn("Log export skipped — no folder selected.");
                    return;
                }

                _vm.LogSaveFolder = dialog.FolderName;
            }

            var fileService = new LogFileService();
            string path = fileService.Write(_vm.LiveLog, _vm.LogSaveFolder);
            _log.Info($"Log saved to {path}");
        }
    }
}
