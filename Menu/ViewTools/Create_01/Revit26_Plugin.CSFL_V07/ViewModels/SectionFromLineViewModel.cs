using System;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CSFL_V07.Enums;
using Revit26_Plugin.CSFL_V07.Models;
using Revit26_Plugin.CSFL_V07.Services.Execution;

namespace Revit26_Plugin.CSFL_V07.ViewModels
{
    /// <summary>
    /// ViewModel for creating section views from detail lines.
    /// Contains ONLY UI state, validation, and user intent.
    /// </summary>
    public partial class SectionFromLineViewModel : ObservableObject
    {
        // ================= EVENTS =================

        /// <summary>
        /// Raised when the user confirms creation.
        /// </summary>
        public event Action CreateRequested;

        /// <summary>
        /// Raised when the dialog should close.
        /// </summary>
        public event Action CloseRequested;

        // ================= DATA SOURCES =================

        public ObservableCollection<ViewFamilyType> SectionTypes { get; }
        public ObservableCollection<View> ViewTemplates { get; }
        public ObservableCollection<SnapSourceMode> SnapSources { get; }

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
        [ObservableProperty] private SnapSourceMode selectedSnapSource;

        // ================= LIVE LOG =================

        /// <summary>
        /// Live UI log bound to the dialog.
        /// </summary>
        public ObservableCollection<LogEntry> LiveLog { get; } = new();

        // ================= EXECUTION =================

        /// <summary>
        /// Controls cancellation of long-running operations.
        /// </summary>
        public ExecutionController Execution { get; } = new();

        // ================= COMMANDS =================

        [RelayCommand]
        private void Create()
        {
            CreateRequested?.Invoke();
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

            // Section types
            SectionTypes = new ObservableCollection<ViewFamilyType>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .Where(v => v.ViewFamily == ViewFamily.Section)
                    .OrderBy(v => v.Name));

            SelectedSectionType = SectionTypes.FirstOrDefault();

            // Section view templates
            ViewTemplates = new ObservableCollection<View>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate && v.ViewType == ViewType.Section)
                    .OrderBy(v => v.Name));

            SelectedTemplate = ViewTemplates.FirstOrDefault();

            // Snap modes
            SnapSources = new ObservableCollection<SnapSourceMode>(
                (SnapSourceMode[])Enum.GetValues(typeof(SnapSourceMode)));

            SelectedSnapSource = SnapSourceMode.HostAndLinked;
        }

        // ================= VALIDATION =================

        /// <summary>
        /// Validates numeric user inputs before any Revit API work begins.
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

            return errorMessage == null;
        }
    }
}
