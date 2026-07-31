using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeSections.V001
{
    public partial class RoofEdgeSectionsViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;
        private readonly RoofEdgeSectionsEventHandler _handler;
        private readonly IList<Element> _selectedRoofs;
        private readonly IList<Element> _skippedNonRoofElements;

        public ObservableCollection<PlannedSection> PlannedSections { get; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public ObservableCollection<string> ViewTemplateNames { get; } = new() { "None" };

        [ObservableProperty]
        private double offsetMm;

        [ObservableProperty]
        private double sectionDepthMm;

        [ObservableProperty]
        private double cropHeightMm;

        [ObservableProperty]
        private double fixedCropWidthMm;

        [ObservableProperty]
        private string cropWidthMode;

        [ObservableProperty]
        private string selectedViewTemplateName;

        [ObservableProperty]
        private string openViewsMode; // "AskMe" / "OpenAll" / "DontOpen"

        [ObservableProperty]
        private int roofsSelectedCount;

        [ObservableProperty]
        private int skippedNonRoofCount;

        [ObservableProperty]
        private int sectionsPlannedCount;

        [ObservableProperty]
        private int alreadyExistCount;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string lastRunSummary = "—";

        private readonly RoofEdgeSectionsSettings _settings;

        public RoofEdgeSectionsViewModel(
            Document doc,
            ExternalEvent externalEvent,
            RoofEdgeSectionsEventHandler handler,
            IList<Element> selectedRoofs,
            IList<Element> skippedNonRoofElements)
        {
            _doc = doc;
            _externalEvent = externalEvent;
            _handler = handler;
            _selectedRoofs = selectedRoofs;
            _skippedNonRoofElements = skippedNonRoofElements;

            _settings = SettingsService.Load();
            offsetMm = _settings.OffsetMm;
            sectionDepthMm = _settings.SectionDepthMm;
            cropHeightMm = _settings.CropHeightMm;
            fixedCropWidthMm = _settings.FixedCropWidthMm;
            cropWidthMode = _settings.CropWidthMode;
            selectedViewTemplateName = _settings.ViewTemplateName;
            openViewsMode = _settings.OpenViewsMode;

            LoadViewTemplates();

            roofsSelectedCount = _selectedRoofs.Count;
            skippedNonRoofCount = _skippedNonRoofElements.Count;

            _handler.OnPlanBuilt = OnPlanBuilt;
            _handler.OnRunComplete = OnRunComplete;

            RequestBuildPlan();
        }

        private void LoadViewTemplates()
        {
            ViewTemplateNames.Clear();
            ViewTemplateNames.Add("None");
            var templates = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .Select(v => v.Name)
                .OrderBy(n => n);

            foreach (string name in templates)
                ViewTemplateNames.Add(name);

            if (!ViewTemplateNames.Contains(selectedViewTemplateName))
                selectedViewTemplateName = "None";
        }

        private void RequestBuildPlan()
        {
            IsBusy = true;
            _handler.RequestedAction = RoofEdgeSectionsAction.BuildPlan;
            _handler.SelectedRoofs = _selectedRoofs;
            _handler.SkippedNonRoofElements = _skippedNonRoofElements;
            _externalEvent.Raise();
        }

        private void OnPlanBuilt(ObservableCollection<PlannedSection> plan, ObservableCollection<LogEntry> log)
        {
            PlannedSections.Clear();
            foreach (var row in plan)
                PlannedSections.Add(row);

            foreach (var entry in log)
                LogEntries.Add(entry);

            SectionsPlannedCount = PlannedSections.Count(p => p.Status == PlannedSectionStatus.Ready);
            AlreadyExistCount = PlannedSections.Count(p => p.Status == PlannedSectionStatus.AlreadyExists);

            IsBusy = false;
            RunCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var row in PlannedSections.Where(r => r.Status == PlannedSectionStatus.Ready))
                row.IsIncluded = true;
        }

        [RelayCommand]
        private void ClearAll()
        {
            foreach (var row in PlannedSections)
                row.IsIncluded = false;
        }

        [RelayCommand]
        private void Refresh()
        {
            RequestBuildPlan();
        }

        private bool CanRun() => !IsBusy && PlannedSections.Any(r => r.IsIncluded && r.Status == PlannedSectionStatus.Ready);

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            IsBusy = true;

            var settingsSnapshot = new RoofEdgeSectionsSettings
            {
                OffsetMm = OffsetMm,
                SectionDepthMm = SectionDepthMm,
                CropHeightMm = CropHeightMm,
                FixedCropWidthMm = FixedCropWidthMm,
                CropWidthMode = CropWidthMode,
                ViewTemplateName = SelectedViewTemplateName,
                OpenViewsMode = OpenViewsMode,
                LastLogExportFolder = _settings.LastLogExportFolder
            };

            ViewTemplateOption templateOption = new() { Name = SelectedViewTemplateName };
            if (SelectedViewTemplateName != "None")
            {
                var templateView = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => v.IsTemplate && v.Name == SelectedViewTemplateName);
                if (templateView != null)
                    templateOption.TemplateId = templateView.Id;
            }

            _handler.RequestedAction = RoofEdgeSectionsAction.RunCreate;
            _handler.Settings = settingsSnapshot;
            _handler.RowsToProcess = PlannedSections.Where(r => r.IsIncluded && r.Status == PlannedSectionStatus.Ready).ToList();
            _handler.ViewTemplate = templateOption;

            SettingsService.Save(settingsSnapshot);

            _externalEvent.Raise();
        }

        private void OnRunComplete(RunResult result, ObservableCollection<LogEntry> log)
        {
            foreach (var entry in log)
                LogEntries.Add(entry);

            LastRunSummary = result.SummaryLine;
            IsBusy = false;
            RunCommand.NotifyCanExecuteChanged();

            RequestOpenViewsIfNeeded?.Invoke(result.CreatedViewIds, OpenViewsMode);

            // Rebuild the plan so already-created rows now show as AlreadyExists / disappear appropriately.
            RequestBuildPlan();
        }

        /// <summary>
        /// Raised so the View can show the "Open Views?" prompt (AskMe mode) or
        /// silently open/skip (OpenAll/DontOpen), keeping Revit UI-thread activation
        /// concerns in the View rather than the ViewModel.
        /// </summary>
        public Action<List<ElementId>, string> RequestOpenViewsIfNeeded { get; set; }

        [RelayCommand]
        private void CopyAllLogs()
        {
            CopyLogsRequested?.Invoke(LogEntries.ToList());
        }

        [RelayCommand]
        private void CopySelectedLogs(IList<object> selectedItems)
        {
            var entries = selectedItems?.Cast<LogEntry>().ToList() ?? new List<LogEntry>();
            CopyLogsRequested?.Invoke(entries);
        }

        [RelayCommand]
        private void ClearLogs()
        {
            LogEntries.Clear();
        }

        public Action<List<LogEntry>> CopyLogsRequested { get; set; }

        [RelayCommand]
        private void ExportLogs()
        {
            ExportLogsRequested?.Invoke(LogEntries.ToList(), _settings.LastLogExportFolder);
        }

        public Action<List<LogEntry>, string> ExportLogsRequested { get; set; }

        public void OnWindowClosing()
        {
            var settingsSnapshot = new RoofEdgeSectionsSettings
            {
                OffsetMm = OffsetMm,
                SectionDepthMm = SectionDepthMm,
                CropHeightMm = CropHeightMm,
                FixedCropWidthMm = FixedCropWidthMm,
                CropWidthMode = CropWidthMode,
                ViewTemplateName = SelectedViewTemplateName,
                OpenViewsMode = OpenViewsMode,
                LastLogExportFolder = _settings.LastLogExportFolder
            };
            SettingsService.Save(settingsSnapshot);

            if (LogEntries.Count > 0 && !string.IsNullOrWhiteSpace(_settings.LastLogExportFolder))
            {
                try { LogExportHelper.Export(LogEntries, _settings.LastLogExportFolder); }
                catch { /* auto-save on close is best-effort, never blocks closing */ }
            }
        }
    }
}
