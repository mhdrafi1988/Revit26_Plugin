using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreateSections.V011.Models;
using Revit26_Plugin.CreateSections.V011.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CreateSections.V011.ViewModels
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
    ///
    /// V009 changes from V008:
    /// - Added View Crop properties: CropBoxActive/CropRegionVisible/
    ///   AnnotationCropActive (all default true) and 4 annotation offset
    ///   fields (default 100mm each). Validated non-negative only — Revit's
    ///   ViewCropRegionShapeManager rejects negative annotation offsets.
    /// - Added PreviewRows (ObservableCollection&lt;SectionPreviewRow&gt;) backing
    ///   the new "Sections To Create" grid — populated by SectionPreviewScanHandler,
    ///   not by this ViewModel directly.
    ///
    /// V010 changes from V009:
    /// - LogSaveFolder no longer prompted via dialog automatically on Create
    ///   completion (see orchestrator). Added BrowseLogFolderRequested event +
    ///   BrowseLogFolderCommand so the user can set a custom folder from the
    ///   UI; the code-behind owns the actual OpenFolderDialog call (MVVM —
    ///   no dialog types in the ViewModel).
    /// </summary>
    public partial class SectionFromLineViewModel : ObservableObject
    {
        // ================= EVENTS =================

        /// <summary>Raised when the user confirms creation.</summary>
        public event Action CreateRequested;

        /// <summary>Raised when the dialog should close.</summary>
        public event Action CloseRequested;

        /// <summary>
        /// V009: new. Raised on dialog open (once) and whenever the user
        /// clicks Refresh in the "Sections To Create" grid toolbar.
        /// Command.cs wires this to SectionPreviewScanHandler.Raise(),
        /// mirroring how CreateRequested is wired to SectionCreationExternalEvent.
        /// </summary>
        public event Action RefreshPreviewRequested;

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

        // ---- View Crop (V009 — new) ----
        [ObservableProperty] private bool cropBoxActive = true;
        [ObservableProperty] private bool cropRegionVisible = true;
        [ObservableProperty] private bool annotationCropActive = true;

        [ObservableProperty] private double topAnnotationOffsetMm = 100;
        [ObservableProperty] private double bottomAnnotationOffsetMm = 100;
        [ObservableProperty] private double leftAnnotationOffsetMm = 100;
        [ObservableProperty] private double rightAnnotationOffsetMm = 100;

        // ---- Host Source checkboxes (V008 — replaces SnapSourceMode) ----
        [ObservableProperty] private bool searchFloorHost = true;
        [ObservableProperty] private bool searchRoofHost = true;
        [ObservableProperty] private bool searchFloorLinked = true;
        [ObservableProperty] private bool searchRoofLinked = true;

        // ---- Threshold fallback (V008 — new) ----
        [ObservableProperty] private bool useThresholdFallback = true;

        // ---- Log export folder (V008 — new, persisted across sessions) ----
        // V010 fix: no longer prompted automatically on Create completion.
        // Empty = silently uses the orchestrator's DefaultLogFolder. User can
        // set a custom folder via the "Browse…" button in the UI, which raises
        // BrowseLogFolderRequested for the code-behind to show a folder picker
        // (kept out of the ViewModel per MVVM — no dialog types in VMs).
        [ObservableProperty] private string logSaveFolder;

        /// <summary>Raised when the user clicks "Browse…" next to the log folder path. Code-behind shows the folder picker and sets LogSaveFolder on confirm.</summary>
        public event Action BrowseLogFolderRequested;

        [RelayCommand]
        private void BrowseLogFolder() => BrowseLogFolderRequested?.Invoke();

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

        // ================= SECTIONS TO CREATE PREVIEW (V009 — new) =================

        /// <summary>
        /// Backing collection for the "Sections To Create" grid. Populated
        /// by SectionPreviewScanHandler — this ViewModel never scans Revit
        /// data itself, only holds the results and the checked/unchecked
        /// state per row.
        ///
        /// V009: CollectionChanged is wired in the constructor so that
        /// IsIncluded toggles on individual rows (not just Select All/Clear)
        /// also refresh IncludedRowCount — otherwise the metric card would
        /// only update after a full Select All/Clear/Refresh, not on a
        /// single row checkbox click.
        /// </summary>
        public ObservableCollection<SectionPreviewRow> PreviewRows { get; } = new();

        [ObservableProperty] private string previewFilterText = string.Empty;

        /// <summary>
        /// Live count of checked rows — feeds the "Selected" metric card
        /// (replacing the old refs.Count-based SelectedCount, since the
        /// grid's checked rows are now the source of truth for Create).
        /// </summary>
        public int IncludedRowCount => PreviewRows.Count(r => r.IsIncluded);

        [RelayCommand]
        private void SelectAllPreviewRows()
        {
            foreach (var row in VisiblePreviewRows())
                row.IsIncluded = true;
            OnPropertyChanged(nameof(IncludedRowCount));
        }

        [RelayCommand]
        private void ClearAllPreviewRows()
        {
            foreach (var row in VisiblePreviewRows())
                row.IsIncluded = false;
            OnPropertyChanged(nameof(IncludedRowCount));
        }

        /// <summary>
        /// V009: rows matching PreviewFilterText (name/level/host, case-insensitive).
        /// Select All / Clear act only on this filtered set, per DATAGRID SPEC
        /// rule 5 ("Select All/Clear scoped to visible/filtered rows").
        /// </summary>
        private IEnumerable<SectionPreviewRow> VisiblePreviewRows()
        {
            if (string.IsNullOrWhiteSpace(PreviewFilterText))
                return PreviewRows;

            string f = PreviewFilterText.Trim();
            return PreviewRows.Where(r =>
                (r.SectionName?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Level?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.HostTypeDisplay?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        [RelayCommand]
        private void RefreshPreview()
        {
            RefreshPreviewRequested?.Invoke();
        }

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
            CropBoxActive = settings.CropBoxActive;
            CropRegionVisible = settings.CropRegionVisible;
            AnnotationCropActive = settings.AnnotationCropActive;
            TopAnnotationOffsetMm = settings.TopAnnotationOffsetMm;
            BottomAnnotationOffsetMm = settings.BottomAnnotationOffsetMm;
            LeftAnnotationOffsetMm = settings.LeftAnnotationOffsetMm;
            RightAnnotationOffsetMm = settings.RightAnnotationOffsetMm;

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

            // V009: keep IncludedRowCount live as rows are added/removed
            // (Refresh clears+repopulates PreviewRows) and as individual
            // rows' IsIncluded checkbox is toggled in the grid.
            PreviewRows.CollectionChanged += (_, e) =>
            {
                if (e.OldItems != null)
                    foreach (SectionPreviewRow row in e.OldItems)
                        row.PropertyChanged -= OnPreviewRowPropertyChanged;

                if (e.NewItems != null)
                    foreach (SectionPreviewRow row in e.NewItems)
                        row.PropertyChanged += OnPreviewRowPropertyChanged;

                OnPropertyChanged(nameof(IncludedRowCount));
            };
        }

        private void OnPreviewRowPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SectionPreviewRow.IsIncluded))
                OnPropertyChanged(nameof(IncludedRowCount));
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
                LogSaveFolder = LogSaveFolder,
                CropBoxActive = CropBoxActive,
                CropRegionVisible = CropRegionVisible,
                AnnotationCropActive = AnnotationCropActive,
                TopAnnotationOffsetMm = TopAnnotationOffsetMm,
                BottomAnnotationOffsetMm = BottomAnnotationOffsetMm,
                LeftAnnotationOffsetMm = LeftAnnotationOffsetMm,
                RightAnnotationOffsetMm = RightAnnotationOffsetMm
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

            // V009: annotation crop offsets — Revit's ViewCropRegionShapeManager
            // requires non-negative values (they only ever expand the annotation
            // crop outward from the model crop; negative is rejected by the API).
            else if (AnnotationCropActive && TopAnnotationOffsetMm < 0)
                errorMessage = "Top Offset cannot be negative — Revit does not support shrinking the annotation crop inward.";

            else if (AnnotationCropActive && BottomAnnotationOffsetMm < 0)
                errorMessage = "Bottom Offset cannot be negative — Revit does not support shrinking the annotation crop inward.";

            else if (AnnotationCropActive && LeftAnnotationOffsetMm < 0)
                errorMessage = "Left Offset cannot be negative — Revit does not support shrinking the annotation crop inward.";

            else if (AnnotationCropActive && RightAnnotationOffsetMm < 0)
                errorMessage = "Right Offset cannot be negative — Revit does not support shrinking the annotation crop inward.";

            return errorMessage == null;
        }

        private bool AnyHostSourceChecked()
            => SearchFloorHost || SearchRoofHost || SearchFloorLinked || SearchRoofLinked;
    }
}
