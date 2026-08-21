using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.DetailLineClosedLoop.V001.Core.Models;
using Revit26_Plugin.DetailLineClosedLoop.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.UI.ViewModels
{
    public partial class DetailLineClosedLoopViewModel : ObservableObject
    {
        private const string ToolFolderName = "DetailLineClosedLoop";

        private readonly ExternalEvent _externalEvent;
        private readonly DetailLineClosedLoopExternalEventHandler _handler;
        private readonly DetailLineClosedLoopSettings _settings;

        /// <summary>Owner window for any secondary dialogs raised from this ViewModel.</summary>
        public System.Windows.Window OwnerWindow { get; set; }

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public List<ElementId> SelectedCurveIds { get; private set; } = new();

        /// <summary>Set by the View's Copy-Selected click handler immediately before invoking the command (ListBox multi-select isn't natively bindable).</summary>
        public IList SelectedLogItemsForCopy { get; set; }

        [ObservableProperty] private int selectedCount;
        [ObservableProperty] private bool snapEndpoints = true;
        [ObservableProperty] private string gapToleranceText = "3.0";
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string runSummary = "Ready.";

        [ObservableProperty] private int inLoopCount;
        [ObservableProperty] private int mergedCount;
        [ObservableProperty] private int removedCount;
        [ObservableProperty] private int gapsClosedCount;
        [ObservableProperty] private int failedCount;

        /// <summary>Resolved gap tolerance for the run currently in flight, in internal feet.</summary>
        public double EffectiveGapToleranceFeet { get; private set; }

        // ── Created Lines grid (populated after a successful Run) ──────────

        public ObservableCollection<CreatedLineItem> CreatedLines { get; } = new();

        [ObservableProperty] private bool groupNewLines;
        [ObservableProperty] private string groupName = "ClosedLoop Group";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredCreatedLines))]
        private string createdLinesFilterText = string.Empty;

        [ObservableProperty] private int createdLinesSelectedCount;

        /// <summary>Header checkbox for the Created Lines grid — true only when every row is checked; setting it checks/unchecks all rows at once.</summary>
        public bool AllCreatedLinesChecked
        {
            get => CreatedLines.Count > 0 && CreatedLines.All(x => x.IsChecked);
            set
            {
                foreach (CreatedLineItem row in CreatedLines)
                    row.IsChecked = value;
                OnPropertyChanged();
            }
        }

        public IEnumerable<CreatedLineItem> FilteredCreatedLines =>
            CreatedLines.Where(x =>
                string.IsNullOrWhiteSpace(CreatedLinesFilterText) ||
                x.TypeName.Contains(CreatedLinesFilterText, StringComparison.OrdinalIgnoreCase) ||
                x.Id.Value.ToString(CultureInfo.InvariantCulture).Contains(CreatedLinesFilterText, StringComparison.OrdinalIgnoreCase));

        public DetailLineClosedLoopViewModel(UIApplication uiApp)
        {
            _settings = SettingsService<DetailLineClosedLoopSettings>.Load(ToolFolderName);
            SnapEndpoints = _settings.SnapEndpoints;
            GapToleranceText = _settings.GapToleranceMm.ToString(CultureInfo.InvariantCulture);
            GroupNewLines = _settings.GroupNewLines;
            GroupName = _settings.GroupName;

            _handler = new DetailLineClosedLoopExternalEventHandler(this);
            _externalEvent = ExternalEvent.Create(_handler);
        }

        private bool CanSelect => !IsBusy;
        private bool CanRun => !IsBusy && SelectedCount > 0;

        [RelayCommand(CanExecute = nameof(CanSelect))]
        private void SelectLines() => RaiseRequest(DetailLineClosedLoopRequest.SelectLines);

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            ResolveEffectiveInputs();
            RaiseRequest(DetailLineClosedLoopRequest.Run);
        }

        private bool CanDeleteSelectedLines => !IsBusy && CreatedLinesSelectedCount > 0;

        [RelayCommand(CanExecute = nameof(CanDeleteSelectedLines))]
        private void DeleteSelectedLines() => RaiseRequest(DetailLineClosedLoopRequest.DeleteSelectedLines);

        [RelayCommand(CanExecute = nameof(CanSelect))]
        private void RefreshCreatedLines() => RaiseRequest(DetailLineClosedLoopRequest.RefreshCreatedLines);

        [RelayCommand]
        private void SelectAllCreatedLines()
        {
            foreach (CreatedLineItem row in FilteredCreatedLines)
                row.IsChecked = true;
        }

        [RelayCommand]
        private void ClearCreatedLinesSelection()
        {
            foreach (CreatedLineItem row in FilteredCreatedLines)
                row.IsChecked = false;
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            string text = string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString()));
            System.Windows.Clipboard.SetText(text);
        }

        [RelayCommand]
        private void CopySelectedLogs()
        {
            if (SelectedLogItemsForCopy == null || SelectedLogItemsForCopy.Count == 0)
                return;

            string text = string.Join(Environment.NewLine, SelectedLogItemsForCopy.Cast<LogEntry>().Select(e => e.ToString()));
            System.Windows.Clipboard.SetText(text);
        }

        private void RaiseRequest(DetailLineClosedLoopRequest request)
        {
            IsBusy = true;
            _handler.PendingRequest = request;
            _externalEvent.Raise();
        }

        private void ResolveEffectiveInputs()
        {
            if (TryParsePositiveDouble(GapToleranceText, out double mm))
            {
                EffectiveGapToleranceFeet = UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
            }
            else
            {
                EffectiveGapToleranceFeet = UnitUtils.ConvertToInternalUnits(_settings.GapToleranceMm, UnitTypeId.Millimeters);
                Log(LogLevel.Info, $"Gap tolerance fallback to default ({_settings.GapToleranceMm}mm) — invalid input.");
            }
        }

        private static bool TryParsePositiveDouble(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            return value > 0;
        }

        // ── Called by the external event handler (same UI thread) ──────────

        public void Log(LogLevel level, string message) => LogEntries.Add(new LogEntry(level, message));

        public void SetSelection(List<ElementId> ids)
        {
            SelectedCurveIds = ids;
            SelectedCount = ids.Count;
            RunCommand.NotifyCanExecuteChanged();
        }

        public void ApplyRunResult(ProcessResult result)
        {
            InLoopCount = result.CurvesInLoop;
            MergedCount = result.MergedCount;
            RemovedCount = result.RemovedCount;
            GapsClosedCount = result.GapsClosedCount;
            FailedCount = result.FailedCount;

            RunSummary = result.Success
                ? $"{result.CurvesInLoop} curves in loop | {result.MergedCount} merged | {result.RemovedCount} removed | {result.GapsClosedCount} gap(s) closed | 0 failed"
                : $"Failed — {result.ErrorMessage}";

            PersistSettings();
        }

        public void PersistSettings()
        {
            _settings.SnapEndpoints = SnapEndpoints;
            if (EffectiveGapToleranceFeet > 0)
                _settings.GapToleranceMm = UnitUtils.ConvertFromInternalUnits(EffectiveGapToleranceFeet, UnitTypeId.Millimeters);
            _settings.GroupNewLines = GroupNewLines;
            _settings.GroupName = string.IsNullOrWhiteSpace(GroupName) ? _settings.GroupName : GroupName.Trim();
            SettingsService<DetailLineClosedLoopSettings>.Save(ToolFolderName, _settings);
        }

        // ── Created Lines grid plumbing — called by the external event handler (same UI thread) ──

        public List<ElementId> GetCheckedCreatedLineIds() =>
            CreatedLines.Where(x => x.IsChecked).Select(x => x.Id).ToList();

        public List<ElementId> GetAllCreatedLineIds() =>
            CreatedLines.Select(x => x.Id).ToList();

        public void SetCreatedLines(List<CreatedLineItem> rows)
        {
            foreach (CreatedLineItem old in CreatedLines)
                old.PropertyChanged -= OnCreatedLineItemPropertyChanged;

            CreatedLines.Clear();
            foreach (CreatedLineItem row in rows)
            {
                row.PropertyChanged += OnCreatedLineItemPropertyChanged;
                CreatedLines.Add(row);
            }

            RecomputeCreatedLinesSelection();
            OnPropertyChanged(nameof(FilteredCreatedLines));
        }

        public void RemoveCreatedLines(IEnumerable<ElementId> ids)
        {
            var idSet = new HashSet<ElementId>(ids);
            List<CreatedLineItem> toRemove = CreatedLines.Where(x => idSet.Contains(x.Id)).ToList();
            foreach (CreatedLineItem row in toRemove)
            {
                row.PropertyChanged -= OnCreatedLineItemPropertyChanged;
                CreatedLines.Remove(row);
            }

            RecomputeCreatedLinesSelection();
            OnPropertyChanged(nameof(FilteredCreatedLines));
        }

        private void OnCreatedLineItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CreatedLineItem.IsChecked))
                RecomputeCreatedLinesSelection();
        }

        private void RecomputeCreatedLinesSelection()
        {
            CreatedLinesSelectedCount = CreatedLines.Count(x => x.IsChecked);
            DeleteSelectedLinesCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(AllCreatedLinesChecked));
        }

        partial void OnIsBusyChanged(bool value)
        {
            SelectLinesCommand.NotifyCanExecuteChanged();
            RunCommand.NotifyCanExecuteChanged();
            DeleteSelectedLinesCommand.NotifyCanExecuteChanged();
            RefreshCreatedLinesCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedCountChanged(int value) => RunCommand.NotifyCanExecuteChanged();
    }
}
