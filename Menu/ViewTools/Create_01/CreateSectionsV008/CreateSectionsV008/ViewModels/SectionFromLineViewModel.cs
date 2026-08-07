using System;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.Models;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.ViewModels
{
    /// <summary>
    /// ViewModel for creating section views from detail lines.
    /// Contains ONLY UI state, validation, and user intent.
    ///
    /// V008 changes from V07:
    /// - SelectedSnapSource dropdown replaced by 4 independent host-source
    ///   checkboxes (SearchFloorHost / SearchRoofHost / SearchFloorLinked /
    ///   SearchRoofLinked), all default true.
    /// - Added UseThresholdFallback (default true) for the no-host fallback path.
    /// - Added SelectedCount/CreatedCount/SkippedCount/FailedCount/RenamedCount
    ///   for the metric cards, plus ResetMetrics().
    /// - Added IsRunning to drive the Create button's disabled/"Running…" state.
    /// - LiveLog now uses the shared Revit26_Plugin.Shared.Models.LogEntry
    ///   instead of the V07 tool-local Brush-coupled LogEntry.
    /// </summary>
    public partial class SectionFromLineViewModel : ObservableObject
    {
        // ================= EVENTS =================

        /// <summary>Raised when the user confirms creation.</summary>
        public event Action CreateRequested;

        /// <summary>Raised when the dialog should close.</summary>
        public event Action CloseRequested;

        // ================= DATA SOURCES =================

        public ObservableCollection<ViewFamilyType> SectionTypes { get; }
        public ObservableCollection<View> ViewTemplates { get; }

        // ================= USER OPTIONS =================

        [ObservableProperty] private string sectionPrefix = "Zone_00_Section";

        [ObservableProperty] private double farClipMm = 10;
        [ObservableProperty] private double searchThresholdMm = 2000;
        [ObservableProperty] private double topPaddingMm = 1000;
        [ObservableProperty] private double bottomPaddingMm = 1000;
        [ObservableProperty] private double bottomOffsetMm = 10;

        [ObservableProperty] private bool openAllAfterCreate = false;
        [ObservableProperty] private bool deleteLinesAfterCreate = false;

        [ObservableProperty] private ViewFamilyType selectedSectionType;
        [ObservableProperty] private View selectedTemplate;

        [ObservableProperty] private int viewScale = 100; // Default 1:100

        // ---- Host Source checkboxes (V008 — replaces SnapSourceMode) ----
        [ObservableProperty] private bool searchFloorHost = true;
        [ObservableProperty] private bool searchRoofHost = true;
        [ObservableProperty] private bool searchFloorLinked = true;
        [ObservableProperty] private bool searchRoofLinked = true;

        // ---- Threshold fallback (V008 — new) ----
        [ObservableProperty] private bool useThresholdFallback = true;

        // ---- Log export folder (V008 — new, persisted across sessions) ----
        [ObservableProperty] private string logSaveFolder;

        // ================= METRICS (V008 — new, for metric cards) =================

        [ObservableProperty] private int selectedCount;
        [ObservableProperty] private int createdCount;
        [ObservableProperty] private int skippedCount;
        [ObservableProperty] private int failedCount;
        [ObservableProperty] private int renamedCount;

        /// <summary>Resets all run metrics to 0. Called at the start of each Create run.</summary>
        public void ResetMetrics()
        {
            SelectedCount = 0;
            CreatedCount = 0;
            SkippedCount = 0;
            FailedCount = 0;
            RenamedCount = 0;
        }

        // ---- Run state (V008 — new, drives Create button disable/"Running…") ----
        [ObservableProperty] private bool isRunning;

        // ================= LIVE LOG =================

        /// <summary>
        /// Live UI log bound to the dialog.
        /// V008: uses the shared LogEntry (Revit26_Plugin.Shared.Models),
        /// not the V07 tool-local Brush-coupled LogEntry.
        /// </summary>
        public ObservableCollection<LogEntry> LiveLog { get; } = new();

        // ================= EXECUTION =================

        /// <summary>Controls cancellation of long-running operations.</summary>
        public ExecutionController Execution { get; } = new();

        // ================= COMMANDS =================

        [RelayCommand(CanExecute = nameof(CanCreate))]
        private void Create()
        {
            CreateRequested?.Invoke();
        }

        private bool CanCreate() => !IsRunning;

        partial void OnIsRunningChanged(bool value)
        {
            CreateCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void CancelDialog()
        {
            Execution.Cancel();
            CloseRequested?.Invoke();
        }

        // ================= CONSTRUCTOR =================

        public SectionFromLineViewModel(Document doc)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));

            // V008: load persisted settings before wiring Revit-derived collections,
            // so SectionPrefix/geometry/checkboxes reflect the user's last session.
            var settings = SettingsService.Load();
            SectionPrefix = settings.SectionPrefix;
            FarClipMm = settings.FarClipMm;
            SearchThresholdMm = settings.SearchThresholdMm;
            TopPaddingMm = settings.TopPaddingMm;
            BottomPaddingMm = settings.BottomPaddingMm;
            BottomOffsetMm = settings.BottomOffsetMm;
            ViewScale = settings.ViewScale;
            SearchFloorHost = settings.SearchFloorHost;
            SearchRoofHost = settings.SearchRoofHost;
            SearchFloorLinked = settings.SearchFloorLinked;
            SearchRoofLinked = settings.SearchRoofLinked;
            UseThresholdFallback = settings.UseThresholdFallback;
            OpenAllAfterCreate = settings.OpenAllAfterCreate;
            DeleteLinesAfterCreate = settings.DeleteLinesAfterCreate;
            LogSaveFolder = settings.LogSaveFolder;

            SectionTypes = new ObservableCollection<ViewFamilyType>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .Where(v => v.ViewFamily == ViewFamily.Section)
                    .OrderBy(v => v.Name));

            var detailSectionType = SectionTypes.FirstOrDefault(v =>
                v.Name.Contains("Detail", StringComparison.OrdinalIgnoreCase));

            SelectedSectionType = detailSectionType ?? SectionTypes.FirstOrDefault();

            ViewTemplates = new ObservableCollection<View>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate && v.ViewType == ViewType.Section)
                    .OrderBy(v => v.Name));

            SelectedTemplate = ViewTemplates.FirstOrDefault();
        }

        /// <summary>
        /// V008: new. Persists current option values to settings.json.
        /// Called on dialog close (see code-behind) and after a successful run.
        /// </summary>
        public void SaveSettings()
        {
            SettingsService.Save(new SectionCreationSettings
            {
                SectionPrefix = SectionPrefix,
                FarClipMm = FarClipMm,
                SearchThresholdMm = SearchThresholdMm,
                TopPaddingMm = TopPaddingMm,
                BottomPaddingMm = BottomPaddingMm,
                BottomOffsetMm = BottomOffsetMm,
                ViewScale = ViewScale,
                SearchFloorHost = SearchFloorHost,
                SearchRoofHost = SearchRoofHost,
                SearchFloorLinked = SearchFloorLinked,
                SearchRoofLinked = SearchRoofLinked,
                UseThresholdFallback = UseThresholdFallback,
                OpenAllAfterCreate = OpenAllAfterCreate,
                DeleteLinesAfterCreate = DeleteLinesAfterCreate,
                LogSaveFolder = LogSaveFolder
            });
        }

        // ================= VALIDATION =================

        /// <summary>
        /// Validates numeric user inputs before any Revit API work begins.
        /// V008: added the "at least one host source checked, unless fallback
        /// is enabled" rule — otherwise every line would be unconditionally
        /// skipped with no way to create anything.
        /// </summary>
        public bool ValidateInputs(out string errorMessage)
        {
            errorMessage = null;

            if (FarClipMm <= 0)
                errorMessage = "Far Clip must be greater than 0 mm.";

            else if (SearchThresholdMm <= 0)
                errorMessage = "Search Threshold must be greater than 0 mm.";

            else if (TopPaddingMm < 0)
                errorMessage = "Top Padding cannot be negative.";

            else if (BottomPaddingMm < 0)
                errorMessage = "Bottom Padding cannot be negative.";

            else if (BottomOffsetMm < 0)
                errorMessage = "Bottom Offset cannot be negative.";

            else if (SelectedSectionType == null)
                errorMessage = "No Section Type selected.";

            else if (ViewScale <= 0)
                errorMessage = "View Scale must be greater than 0.";

            else if (!AnyHostSourceChecked() && !UseThresholdFallback)
                errorMessage = "No Host Source category is checked, and threshold fallback is off — nothing could ever be created. Check a source or enable the fallback.";

            return errorMessage == null;
        }

        private bool AnyHostSourceChecked()
            => SearchFloorHost || SearchRoofHost || SearchFloorLinked || SearchRoofLinked;
    }
}
