// =======================================================
// File: RoofDetailLineIntersectViewModel.cs
// Location: UI/ViewModels/
// Changes vs V008:
//   MOVED all geometry/placement logic out to
//   Core/Engine/RoofDetailLineIntersectEngine.cs (see that file's
//   header) — this suite's vertical-slice convention keeps Core/Engine
//   logic out of UI/ViewModels.
//   The private inner IExternalEventHandler class + directly-held
//   Document/FootPrintRoof/List<DetailLine> fields are replaced by the
//   standard Payload/Handler/EventManager pattern used by every other
//   tool — RoofId/DetailLineIds (ElementIds) are carried across the
//   boundary and re-resolved inside the Handler, rather than holding
//   live Element references across multiple Run clicks.
//   [RelayCommand] attributes replace the hand-wired
//   ICommand/RelayCommand-in-constructor pattern (was already
//   ObservableObject-based, just not using the attributes).
// =======================================================

using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofDetailLineIntersect.V011.Core.Models;
using Revit26_Plugin.RoofDetailLineIntersect.V011.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace Revit26_Plugin.RoofDetailLineIntersect.V011.UI.ViewModels
{
    public partial class RoofDetailLineIntersectViewModel : ObservableObject
    {
        private readonly ElementId _roofId;
        private readonly List<ElementId> _detailLineIds;

        [ObservableProperty] private string roofName   = string.Empty;
        [ObservableProperty] private string roofIdText = string.Empty;
        [ObservableProperty] private string viewName   = string.Empty;
        [ObservableProperty] private int    totalLines;
        [ObservableProperty] private int    intersectionsFound;
        [ObservableProperty] private int    pointsPlaced;
        [ObservableProperty] private int    skippedCount;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private bool isBusy;

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public RoofDetailLineIntersectViewModel(
            ElementId roofId, List<ElementId> detailLineIds, string roofName, string viewName)
        {
            _roofId        = roofId;
            _detailLineIds = detailLineIds;

            RoofName   = roofName ?? "Roof";
            RoofIdText = $"id {roofId.Value}";
            ViewName   = viewName ?? string.Empty;
            TotalLines = detailLineIds.Count;

            RoofDetailLineIntersectEventManager.Init();
        }

        // ── Trigger ExternalEvent ─────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            IsBusy = true;
            LogEntries.Clear();
            IntersectionsFound = 0;
            PointsPlaced       = 0;
            SkippedCount       = 0;

            RoofDetailLineIntersectHandler.Payload = new RoofDetailLineIntersectPayload
            {
                RoofId        = _roofId,
                DetailLineIds = _detailLineIds,
                Log           = AddLog,
                OnCompleted   = result =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        IsBusy = false;

                        if (!result.Success)
                        {
                            AddLog(LogLevel.Error, $"Run failed: {result.ErrorMessage}");
                            return;
                        }

                        IntersectionsFound = result.IntersectionsFound;
                        PointsPlaced       = result.PointsPlaced;
                        SkippedCount       = result.SkippedCount;
                    }));
                }
            };

            try
            {
                RoofDetailLineIntersectEventManager.Event.Raise();
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Fatal error raising event: {ex.Message}");
                IsBusy = false;
            }
        }

        private bool CanRun() => !IsBusy;

        // ── Log & Copy ────────────────────────────────────────────────────────
        [RelayCommand]
        private void CopyLog()
        {
            if (LogEntries.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var entry in LogEntries)
                sb.AppendLine(entry.ToString());
            Clipboard.SetText(sb.ToString());
        }

        private void AddLog(LogLevel level, string message) => AddLog(new LogEntry(level, message));

        private void AddLog(LogEntry entry)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                LogEntries.Add(entry);
            else
                dispatcher.BeginInvoke(new Action(() => LogEntries.Add(entry)));
        }
    }
}
