using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.ExternalEvents;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Models;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.ViewModels
{
    /// <summary>
    /// V005: roof pick and drain-point pick now happen in
    /// RoofDrainCalloutPlacingCommand.Execute(), synchronously, before this
    /// ViewModel (and the window) is even constructed — confirmed with Rafi.
    /// This ViewModel is therefore display + parameters + Run only: SelectedRoof
    /// and PickedPoints are populated once, in the constructor, from what the
    /// Command already picked. No PickRoofCommand/PickPointsCommand, no
    /// IsBusyPicking, no re-pick affordance in this version.
    /// </summary>
    public partial class RoofDrainCalloutPlacingViewModel : ObservableObject
    {
        private readonly ExternalEvent _runEvent;
        private readonly Document _doc;
        private readonly SettingsService _settingsService;

        public ObservableCollection<View> DraftingViews { get; } = new ObservableCollection<View>();
        public ObservableCollection<LogEntry> Logs { get; } = new ObservableCollection<LogEntry>();

        /// <summary>Drain points picked on SelectedRoof by the Command, before this ViewModel existed. Read-only display in this version — no add/remove/undo affordance.</summary>
        public ObservableCollection<CandidatePoint> PickedPoints { get; }

        [ObservableProperty] private RoofBase selectedRoof;
        [ObservableProperty] private View selectedDraftingView;
        [ObservableProperty] private string groupingToleranceMmText = "500";
        [ObservableProperty] private string calloutFloorMmText = "500";
        [ObservableProperty] private string duplicateToleranceMmText = "300";
        [ObservableProperty] private string snapToleranceMmText = "150";
        [ObservableProperty] private bool isBusy;

        /// <summary>Set true the moment Run is first clicked and never reset — Run stays
        /// disabled permanently after that, even once IsBusy returns to false on completion.</summary>
        [ObservableProperty] private bool hasRun;

        [ObservableProperty] private int metricRoofs;
        [ObservableProperty] private int metricSourcePoints;
        [ObservableProperty] private int metricGroups;
        [ObservableProperty] private int metricCallouts;

        public double GroupingToleranceMm =>
            double.TryParse(GroupingToleranceMmText, out var v) ? v : 500;

        /// <summary>Fixed callout size (mm) — every callout renders at exactly this width and height, centered on its cluster's centroid.</summary>
        public double CalloutFloorMm =>
            double.TryParse(CalloutFloorMmText, out var v) ? v : 500;

        public double DuplicateToleranceMm =>
            double.TryParse(DuplicateToleranceMmText, out var v) ? v : 300;

        /// <summary>Snap tolerance used during the Command's picking pass, shown here read-only for reference (picking already happened — this no longer drives anything in the window).</summary>
        public double SnapToleranceMm =>
            double.TryParse(SnapToleranceMmText, out var v) ? v : 150;

        /// <summary>Run is only meaningful once at least one drain point exists (roof is always non-null by the time this ViewModel is constructed — see Command), a drafting view is chosen, no run is already in progress, and Run has not already been clicked once (HasRun locks it permanently after first click).</summary>
        public bool CanRun => PickedPoints.Count > 0 && SelectedDraftingView != null && !IsBusy && !HasRun;

        /// <summary>
        /// roof and pickedPoints come from RoofDrainCalloutPlacingCommand, which
        /// already ran Selection.PickObject + the PickPoint loop before
        /// constructing this ViewModel. settings/settingsService are passed in
        /// too so the Command's pre-load (needed for the snap tolerance used
        /// during picking) isn't wastefully repeated here.
        /// </summary>
        public RoofDrainCalloutPlacingViewModel(
            UIApplication uiApp,
            RoofBase roof,
            List<CandidatePoint> pickedPoints,
            RoofDrainCalloutSettings settings,
            SettingsService settingsService)
        {
            _doc = uiApp.ActiveUIDocument.Document;
            _settingsService = settingsService;

            SelectedRoof = roof;
            PickedPoints = new ObservableCollection<CandidatePoint>(pickedPoints);

            // ExternalEvent.Create() must happen here, in the ViewModel constructor,
            // which itself is constructed during IExternalCommand.Execute() — i.e.
            // inside the valid API context. Never create lazily from UI interaction.
            _runEvent = ExternalEvent.Create(new RoofDrainCalloutRunHandler(this));

            LoadDraftingViews();
            ApplySettings(settings);

            Logs.Add(new LogEntry(LogLevel.Info, $"Roof {roof.Id} selected — {PickedPoints.Count} drain point(s) picked"));
        }

        private void LoadDraftingViews()
        {
            var collector = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .Where(v => !v.IsTemplate);

            foreach (var v in collector)
                DraftingViews.Add(v);

            SelectedDraftingView = DraftingViews.FirstOrDefault();
        }

        private void ApplySettings(RoofDrainCalloutSettings settings)
        {
            GroupingToleranceMmText = settings.GroupingToleranceMmText;
            CalloutFloorMmText = settings.CalloutFloorMmText;
            DuplicateToleranceMmText = settings.DuplicateToleranceMmText;
            SnapToleranceMmText = settings.SnapToleranceMmText;

            // Drafting view selection re-resolved by UniqueId — ElementId is not
            // stable across sessions.
            if (!string.IsNullOrEmpty(settings.SelectedDraftingViewUniqueId))
            {
                var match = DraftingViews.FirstOrDefault(v => v.UniqueId == settings.SelectedDraftingViewUniqueId);
                if (match != null) SelectedDraftingView = match;
            }
        }

        public void SaveSettings()
        {
            var settings = new RoofDrainCalloutSettings
            {
                GroupingToleranceMmText = GroupingToleranceMmText,
                CalloutFloorMmText = CalloutFloorMmText,
                DuplicateToleranceMmText = DuplicateToleranceMmText,
                SnapToleranceMmText = SnapToleranceMmText,
                SelectedDraftingViewUniqueId = SelectedDraftingView?.UniqueId
            };
            _settingsService.Save(settings);
        }

        partial void OnSelectedDraftingViewChanged(View value)
        {
            OnPropertyChanged(nameof(CanRun));
            RunCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanRun));
            RunCommand.NotifyCanExecuteChanged();
        }

        partial void OnHasRunChanged(bool value)
        {
            OnPropertyChanged(nameof(CanRun));
            RunCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            HasRun = true;
            IsBusy = true;
            _runEvent.Raise();
        }

        /// <summary>Called by the Run handler on completion — updates are marshalled fine since collections are ObservableCollection bound via WPF's dispatcher.</summary>
        public void ReportResults(int roofs, int sourcePoints, int groups, int callouts)
        {
            MetricRoofs = roofs;
            MetricSourcePoints = sourcePoints;
            MetricGroups = groups;
            MetricCallouts = callouts;
            IsBusy = false;

            SaveSettings();
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            var sb = new StringBuilder();
            foreach (var entry in Logs)
                sb.AppendLine(entry.ToString());
            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());
        }

        [RelayCommand]
        private void ClearLogs()
        {
            Logs.Clear();
        }

        /// <summary>Called from the Window's code-behind with the log ListBox's SelectedItems
        /// (WPF doesn't bind ListBox.SelectedItems directly, so this is invoked on click
        /// rather than exposed as a RelayCommand).</summary>
        public void CopySelectedLogs(System.Collections.IList selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var item in selectedItems)
                if (item is LogEntry entry)
                    sb.AppendLine(entry.ToString());

            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());
        }
    }
}
