using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.SheetViewArrange.V001.Core.Layout;
using Revit26_Plugin.SheetViewArrange.V001.Core.Models;
using Revit26_Plugin.SheetViewArrange.V001.Core.Services;
using Revit26_Plugin.SheetViewArrange.V001.Infrastructure;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.SheetViewArrange.V001.UI.ViewModels
{
    /// <summary>A choice in the "Last row" combo box.</summary>
    public sealed record LastRowOption(LastRowMode Mode, string Label);

    /// <summary>
    /// Window state for Sheet View Arrange. Holds the latest <see cref="SheetSnapshot"/> and
    /// rebuilds the <see cref="ArrangePlan"/> in memory on every settings change, so the preview
    /// is live. All Revit access goes through <see cref="ISheetArrangeSession"/>.
    /// </summary>
    public partial class SheetViewArrangeViewModel : ObservableObject, IDisposable
    {
        private const string SettingsFolder = "SheetViewArrange";

        private readonly ISheetArrangeSession _session;
        private SheetSnapshot _snapshot;

        /// <summary>Raised whenever <see cref="Plan"/> is rebuilt (the window redraws its preview).</summary>
        public event EventHandler PlanChanged;

        /// <summary>Grid rows of the current plan.</summary>
        public ObservableCollection<ArrangeRow> Rows { get; } = new();

        /// <summary>Activity log.</summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        /// <summary>Choices for the last-row combo box.</summary>
        public IReadOnlyList<LastRowOption> LastRowOptions { get; } = new[]
        {
            new LastRowOption(LastRowMode.PackLeft, "Pack left (same gap as row above)"),
            new LastRowOption(LastRowMode.Justify, "Justify across full width"),
            new LastRowOption(LastRowMode.Center, "Centre (same gap as row above)")
        };

        /// <summary>Current plan; never null after construction.</summary>
        public ArrangePlan Plan { get; private set; }

        [ObservableProperty] private string sheetLabel = "";
        [ObservableProperty] private double marginTopMm;
        [ObservableProperty] private double marginBottomMm;
        [ObservableProperty] private double marginLeftMm;
        [ObservableProperty] private double marginRightMm;
        [ObservableProperty] private double minHorizontalGapMm;
        [ObservableProperty] private double minVerticalGapMm;
        [ObservableProperty] private LastRowMode lastRowMode;
        [ObservableProperty] private bool movePinned;

        [ObservableProperty] private int viewCount;
        [ObservableProperty] private int rowCount;
        [ObservableProperty] private int moveCount;
        [ObservableProperty] private int skippedCount;
        [ObservableProperty] private string statusText = "";
        [ObservableProperty] private bool hasBlocker;
        [ObservableProperty] private string warningsText = "";
        [ObservableProperty] private bool hasWarnings;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(RefreshCommand))]
        private bool isBusy;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
        private bool canApply;

        /// <summary>Creates the view model over <paramref name="session"/> and plans the initial sheet.</summary>
        public SheetViewArrangeViewModel(ISheetArrangeSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            // Settings first; Recompute() ignores the On*Changed calls this fires (no snapshot yet).
            var s = SettingsService<SheetViewArrangeSettings>.Load(SettingsFolder);
            MarginTopMm = s.MarginTopMm;
            MarginBottomMm = s.MarginBottomMm;
            MarginLeftMm = s.MarginLeftMm;
            MarginRightMm = s.MarginRightMm;
            MinHorizontalGapMm = s.MinHorizontalGapMm;
            MinVerticalGapMm = s.MinVerticalGapMm;
            LastRowMode = Enum.IsDefined(typeof(LastRowMode), s.LastRowMode) ? s.LastRowMode : LastRowMode.PackLeft;
            MovePinned = s.MovePinned;

            LoadSnapshot(session.Initial, "Loaded");
        }

        /// <summary>Settings as currently shown in the window.</summary>
        public SheetViewArrangeSettings CurrentSettings() => new()
        {
            MarginTopMm = MarginTopMm,
            MarginBottomMm = MarginBottomMm,
            MarginLeftMm = MarginLeftMm,
            MarginRightMm = MarginRightMm,
            MinHorizontalGapMm = MinHorizontalGapMm,
            MinVerticalGapMm = MinVerticalGapMm,
            LastRowMode = LastRowMode,
            MovePinned = MovePinned
        };

        /// <summary>Writes the current settings to disk (called when the window closes).</summary>
        public void SaveSettings() => SettingsService<SheetViewArrangeSettings>.Save(SettingsFolder, CurrentSettings());

        // ── Settings changes → live re-plan ─────────────────────────────────
        partial void OnMarginTopMmChanged(double value) => ClampOrRecompute(value, v => MarginTopMm = v);
        partial void OnMarginBottomMmChanged(double value) => ClampOrRecompute(value, v => MarginBottomMm = v);
        partial void OnMarginLeftMmChanged(double value) => ClampOrRecompute(value, v => MarginLeftMm = v);
        partial void OnMarginRightMmChanged(double value) => ClampOrRecompute(value, v => MarginRightMm = v);
        partial void OnMinHorizontalGapMmChanged(double value) => ClampOrRecompute(value, v => MinHorizontalGapMm = v);
        partial void OnMinVerticalGapMmChanged(double value) => ClampOrRecompute(value, v => MinVerticalGapMm = v);
        partial void OnLastRowModeChanged(LastRowMode value) => Recompute();
        partial void OnMovePinnedChanged(bool value) => Recompute();

        /// <summary>Negative or non-finite lengths are reset to 0 (which re-enters and recomputes).</summary>
        private void ClampOrRecompute(double value, Action<double> set)
        {
            if (!double.IsFinite(value) || value < 0)
                set(0);
            else
                Recompute();
        }

        /// <summary>Takes a fresh snapshot; <paramref name="verb"/> non-null logs it with the plan's warnings.</summary>
        private void LoadSnapshot(SheetSnapshot snapshot, string verb)
        {
            _snapshot = snapshot;
            SheetLabel = snapshot.SheetLabel;
            Recompute();

            if (verb == null)
                return;
            Log(LogLevel.Info, $"{verb} sheet {snapshot.SheetLabel}: {snapshot.Viewports.Count} view(s).");
            foreach (var w in Plan.Warnings)
                Log(LogLevel.Warning, w);
            if (Plan.Blocker != null)
                Log(LogLevel.Warning, Plan.Blocker);
        }

        private void Recompute()
        {
            if (_snapshot == null)
                return;

            Plan = ArrangePlanner.Build(_snapshot, CurrentSettings());

            Rows.Clear();
            foreach (var row in Plan.Rows)
                Rows.Add(row);

            ViewCount = Plan.Moves.Count;
            RowCount = Plan.RowCount;
            MoveCount = Plan.MoveCount;
            SkippedCount = Plan.Skipped.Count;
            HasBlocker = Plan.Blocker != null;
            CanApply = Plan.CanApply;
            WarningsText = string.Join(Environment.NewLine, Plan.Warnings.Select(w => "• " + w));
            HasWarnings = Plan.Warnings.Count > 0;
            StatusText = Plan.Blocker
                ?? (Plan.MoveCount == 0
                    ? "Already arranged — every view is in its place."
                    : $"Ready: {Plan.MoveCount} of {Plan.Moves.Count} view(s) move into {Plan.RowCount} row(s).");

            PlanChanged?.Invoke(this, EventArgs.Empty);
        }

        // ── Commands ────────────────────────────────────────────────────────

        /// <summary>Re-reads the sheet from the model (after edits made in Revit).</summary>
        [RelayCommand(CanExecute = nameof(CanRefresh))]
        private void Refresh() => Run(done => _session.Refresh(done), "Refreshed");

        private bool CanRefresh() => !IsBusy;

        /// <summary>Moves the views as previewed — refused by the session if the sheet changed meanwhile.</summary>
        [RelayCommand(CanExecute = nameof(CanRunApply))]
        private void Apply()
        {
            string signature = Plan.Signature;
            var settings = CurrentSettings();
            Run(done => _session.Apply(settings, signature, done), null);
        }

        private bool CanRunApply() => !IsBusy && CanApply;

        /// <summary>Copies every log line to the clipboard.</summary>
        [RelayCommand]
        private void CopyAllLogs() => LogClipboardService.CopyAll(LogEntries);

        /// <summary>Sends one request to Revit, showing the busy state until it comes back.</summary>
        private void Run(Func<Action<SessionResult>, bool> request, string loadVerb)
        {
            IsBusy = true;
            bool accepted = request(result =>
            {
                IsBusy = false;
                if (result.Message != null)
                    Log(result.Level, result.Message);
                if (result.Snapshot != null)
                    LoadSnapshot(result.Snapshot, loadVerb);
            });

            if (!accepted)
            {
                IsBusy = false;
                Log(LogLevel.Error, "Revit did not accept the request. Finish any active command and try again.");
            }
        }

        private void Log(LogLevel level, string message) => LogEntries.Add(new LogEntry(level, message));

        /// <summary>Releases the Revit session.</summary>
        public void Dispose() => _session.Dispose();
    }
}
