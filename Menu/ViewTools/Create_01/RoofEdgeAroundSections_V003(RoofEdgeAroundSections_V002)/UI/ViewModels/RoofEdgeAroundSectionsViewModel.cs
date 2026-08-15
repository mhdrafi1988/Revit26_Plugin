using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeAroundSections.V003
{
    /// <summary>
    /// Presents one enabled/disabled, reorderable naming token as a bindable chip
    /// for the Section Naming Pattern card. Wraps a NamingToken so the UI can toggle
    /// IsEnabled and move order without mutating settings directly on every click.
    /// </summary>
    public partial class NamingTokenChipViewModel : ObservableObject
    {
        public NamingTokenType Type { get; }
        public string DisplayLabel { get; }

        [ObservableProperty]
        private bool isEnabled;

        [ObservableProperty]
        private int order;

        public NamingTokenChipViewModel(NamingTokenType type, string displayLabel, bool isEnabled, int order)
        {
            Type = type;
            DisplayLabel = displayLabel;
            this.isEnabled = isEnabled;
            this.order = order;
        }
    }

    public partial class RoofEdgeSectionsViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;
        private readonly RoofEdgeSectionsEventHandler _handler;
        private readonly IList<Element> _selectedRoofs;
        private readonly IList<Element> _skippedNonRoofElements;
        private readonly RoofEdgeSectionsSettings _settings;

        public ObservableCollection<PlannedSection> PlannedSections { get; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();
        public ObservableCollection<string> ViewTemplateNames { get; } = new() { "None" };
        public ObservableCollection<NamingTokenChipViewModel> NamingTokenChips { get; } = new();

        [ObservableProperty]
        private double offsetMm;

        [ObservableProperty]
        private double searchDistanceMm;

        [ObservableProperty]
        private double marginOffsetMm;

        [ObservableProperty]
        private double edgeDepthMm;

        [ObservableProperty]
        private double cropHeightMm;

        [ObservableProperty]
        private string selectedViewTemplateName;

        [ObservableProperty]
        private string openViewsMode; // "AskMe" / "OpenAll" / "DontOpen"

        [ObservableProperty]
        private bool mergeEnabled;

        [ObservableProperty]
        private double mergeDistanceMm;

        [ObservableProperty]
        private string namingSeparator;

        [ObservableProperty]
        private string namingPreviewText = "";

        // Selection Summary — 5 metrics
        [ObservableProperty]
        private int totalRoofsCount;

        [ObservableProperty]
        private int sectionsDetectedCount;

        [ObservableProperty]
        private int sectionsSuggestedCount;

        [ObservableProperty]
        private int finallyCreatedCount;

        [ObservableProperty]
        private int skippedCount;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string lastRunSummary = "—";

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
            searchDistanceMm = _settings.SearchDistanceMm;
            marginOffsetMm = _settings.MarginOffsetMm;
            edgeDepthMm = _settings.EdgeDepthMm;
            cropHeightMm = _settings.CropHeightMm;
            selectedViewTemplateName = _settings.ViewTemplateName;
            openViewsMode = _settings.OpenViewsMode;
            mergeEnabled = _settings.MergeEnabled;
            mergeDistanceMm = _settings.MergeDistanceMm;
            namingSeparator = _settings.NamingSeparator;

            LoadViewTemplates();
            LoadNamingTokenChips();
            UpdateNamingPreview();

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

        private static readonly Dictionary<NamingTokenType, string> TokenLabels = new()
        {
            [NamingTokenType.Zone] = "Zone",
            [NamingTokenType.Level] = "Level",
            [NamingTokenType.Name] = "Name",
            [NamingTokenType.LineOfDirection] = "Line of Direction",
            [NamingTokenType.Area] = "Area",
            [NamingTokenType.Number] = "Number"
        };

        private void LoadNamingTokenChips()
        {
            NamingTokenChips.Clear();

            // Defensive: settings.NamingTokens comes from a deserialized JSON file that could
            // in principle be hand-edited or stale from a future/older schema. A missing or
            // unrecognized token would otherwise throw here and prevent the window from
            // opening at all — fall back to NamingToken.Defaults() if the loaded set looks
            // invalid, and use a safe label lookup per-token as a second line of defense.
            List<NamingToken> tokensToLoad = _settings.NamingTokens != null && _settings.NamingTokens.Count > 0
                ? _settings.NamingTokens
                : NamingToken.Defaults();

            foreach (NamingToken token in tokensToLoad.OrderBy(t => t.Order))
            {
                string label = TokenLabels.TryGetValue(token.Type, out string knownLabel)
                    ? knownLabel
                    : token.Type.ToString(); // safe fallback — still renders, just less pretty

                var chip = new NamingTokenChipViewModel(token.Type, label, token.IsEnabled, token.Order);
                chip.PropertyChanged += (s, e) => UpdateNamingPreview();
                NamingTokenChips.Add(chip);
            }
        }

        [RelayCommand]
        private void ToggleNamingToken(NamingTokenChipViewModel chip)
        {
            if (chip == null) return;
            chip.IsEnabled = !chip.IsEnabled;
            UpdateNamingPreview();
        }

        /// <summary>
        /// Builds a sample preview string from the current token chip state, using
        /// representative placeholder values (not a real roof) so the pattern can be
        /// previewed before a plan is built.
        /// </summary>
        private void UpdateNamingPreview()
        {
            var parts = new List<string>();
            foreach (var chip in NamingTokenChips.Where(c => c.IsEnabled).OrderBy(c => c.Order))
            {
                string sample = chip.Type switch
                {
                    NamingTokenType.Zone => "ZoneA",
                    NamingTokenType.Level => "Level1",
                    NamingTokenType.Name => "Roof123",
                    NamingTokenType.LineOfDirection => "Line of North",
                    NamingTokenType.Area => "120m2",
                    NamingTokenType.Number => "01",
                    _ => ""
                };
                parts.Add(sample);
            }

            NamingPreviewText = string.Join(string.IsNullOrEmpty(NamingSeparator) ? "_" : NamingSeparator, parts)
                .Replace(" ", string.IsNullOrEmpty(NamingSeparator) ? "_" : NamingSeparator);
        }

        partial void OnNamingSeparatorChanged(string value) => UpdateNamingPreview();

        /// <summary>Builds a settings snapshot from current bindable properties, including naming tokens.</summary>
        private RoofEdgeSectionsSettings BuildSettingsSnapshot()
        {
            return new RoofEdgeSectionsSettings
            {
                OffsetMm = OffsetMm,
                SearchDistanceMm = SearchDistanceMm,
                MarginOffsetMm = MarginOffsetMm,
                EdgeDepthMm = EdgeDepthMm,
                CropHeightMm = CropHeightMm,
                ViewTemplateName = SelectedViewTemplateName,
                OpenViewsMode = OpenViewsMode,
                MergeEnabled = MergeEnabled,
                MergeDistanceMm = MergeDistanceMm,
                NamingSeparator = NamingSeparator,
                NamingTokens = NamingTokenChips
                    .Select(c => new NamingToken { Type = c.Type, IsEnabled = c.IsEnabled, Order = c.Order })
                    .ToList(),
                LastLogExportFolder = _settings.LastLogExportFolder
            };
        }

        private void RequestBuildPlan()
        {
            IsBusy = true;
            _handler.RequestedAction = RoofEdgeSectionsAction.BuildPlan;
            _handler.SelectedRoofs = _selectedRoofs;
            _handler.SkippedNonRoofElements = _skippedNonRoofElements;
            _handler.Settings = BuildSettingsSnapshot(); // needed pre-Run too: merge + naming both read settings during plan-build
            _externalEvent.Raise();
        }

        private void OnPlanBuilt(SectionPlanBuilder.PlanBuildResult result, ObservableCollection<LogEntry> log)
        {
            PlannedSections.Clear();
            foreach (var row in result.Plan)
                PlannedSections.Add(row);

            foreach (var entry in log)
                LogEntries.Add(entry);

            TotalRoofsCount = result.TotalRoofsCount;
            SectionsDetectedCount = result.DetectedCount;
            SectionsSuggestedCount = result.SuggestedCount;

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

            var settingsSnapshot = BuildSettingsSnapshot();

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
            FinallyCreatedCount = result.CreatedCount;
            SkippedCount = result.SkippedCount;

            IsBusy = false;
            RunCommand.NotifyCanExecuteChanged();

            RequestOpenViewsIfNeeded?.Invoke(result.CreatedViewIds, OpenViewsMode);

            // Rebuild the plan so just-created rows drop out (their views now exist) and
            // the Selection Summary reflects the post-Run state.
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
            var settingsSnapshot = BuildSettingsSnapshot();
            SettingsService.Save(settingsSnapshot);

            if (LogEntries.Count > 0 && !string.IsNullOrWhiteSpace(_settings.LastLogExportFolder))
            {
                try { LogExportHelper.Export(LogEntries, _settings.LastLogExportFolder); }
                catch { /* auto-save on close is best-effort, never blocks closing */ }
            }
        }
    }
}
