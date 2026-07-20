using System;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V004
{
    /// <summary>Runs either a floor-creation pass or a roof-creation pass, depending on
    /// which button the ViewModel set on CreateRunRequest.Mode. Same per-room
    /// dedupe/trim/validate boundary logic feeds both. Each room now targets its own
    /// mapped host Level (RoomCandidate.SelectedHostLevel) instead of one shared level —
    /// unmapped rooms are filtered out by the ViewModel before the request is built, so
    /// every candidate reaching here is guaranteed to have a level.</summary>
    public class RunCreateElementsExternalEventHandler : IExternalEventHandler
    {
        public MainViewModel ViewModel { get; set; }
        public CreateRunRequest PendingRequest { get; set; }

        /// <summary>Set by the ViewModel immediately before Raise() — count of selected
        /// rows skipped for having no New Level mapping, folded into the completion summary.</summary>
        public int UnmappedSkippedCount { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var request = PendingRequest;
            if (request == null) return;

            bool isRoof = request.Mode == CreationMode.Roof;
            string verb = isRoof ? "roof" : "floor";

            var summary = new RunSummary { UnmappedSkippedCount = UnmappedSkippedCount };
            bool wasCancelled = false;

            using var tx = new Transaction(doc, isRoof ? "Create roofs from linked rooms" : "Create floors from linked rooms");
            var failureOptions = tx.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new FloorFailuresPreprocessor());
            tx.SetFailureHandlingOptions(failureOptions);
            tx.Start();

            int processed = 0;
            foreach (var candidate in request.Rooms)
            {
                if (request.Cancel != null && request.Cancel.IsCancelled)
                {
                    wasCancelled = true;
                    ViewModel.AddLog(LogLevel.Warning, "Run cancelled by user — remaining rooms skipped.");
                    break;
                }

                // Defensive check: the ViewModel filters unmapped rows before building the
                // request, but if this ever runs with a null mapping (e.g. future caller),
                // skip rather than NRE on candidate.SelectedHostLevel.LevelElement.
                if (candidate.SelectedHostLevel?.LevelElement == null)
                {
                    // Not counted into summary.UnmappedSkippedCount here — that field is
                    // already seeded once from the ViewModel's pre-filter count (see
                    // UnmappedSkippedCount property above). This branch is a defensive
                    // fallback that should never fire in normal operation, so it's counted
                    // as a failure instead to avoid double-counting the same room.
                    summary.FailedCount++;
                    ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped unexpectedly: no New Level mapped (should have been filtered before run).");
                    processed++;
                    ViewModel.ReportProgress(processed);
                    continue;
                }

                var targetLevel = candidate.SelectedHostLevel.LevelElement;

                using var subTx = new SubTransaction(doc);
                subTx.Start();

                try
                {
                    var boundary = RoomBoundaryService.BuildLoops(candidate.RoomElement, request.LinkTransform);

                    if (!boundary.OuterValid)
                    {
                        subTx.RollBack();
                        summary.FailedCount++;
                        ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: {boundary.FailureReason}");
                        continue;
                    }

                    doc.Regenerate();

                    if (isRoof)
                        RoofCreationService.Create(doc, boundary.Loops[0], request.TypeId, targetLevel);
                    else
                        FloorCreationService.Create(doc, boundary.Loops, request.TypeId, targetLevel);

                    doc.Regenerate();

                    subTx.Commit();
                    summary.SuccessCount++;

                    if (boundary.WasTrimmedOrFixed)
                    {
                        summary.TrimmedFixedCount++;
                        ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — {verb} created on '{targetLevel.Name}' (boundary trimmed/fixed)");
                    }
                    else
                    {
                        ViewModel.AddLog(LogLevel.Success, $"{candidate.DisplayName} — {verb} created on '{targetLevel.Name}'");
                    }

                    if (isRoof && boundary.Loops.Count > 1)
                    {
                        // Roofs only ever get the outer loop (see RoofCreationService) — every
                        // inner loop present counts as skipped here, unlike floors.
                        int skipped = boundary.Loops.Count - 1;
                        summary.InnerLoopsSkippedCount += skipped;
                        ViewModel.AddLog(LogLevel.Warning,
                            $"{candidate.DisplayName} — {skipped} inner loop(s) not supported for roofs, outer boundary used");
                    }
                    else if (boundary.InnerLoopsSkipped > 0)
                    {
                        summary.InnerLoopsSkippedCount += boundary.InnerLoopsSkipped;
                        ViewModel.AddLog(LogLevel.Warning,
                            $"{candidate.DisplayName} — {boundary.InnerLoopsSkipped} inner loop(s) skipped, outer boundary used");
                    }
                }
                catch (Exception ex)
                {
                    if (subTx.GetStatus() == TransactionStatus.Started)
                        subTx.RollBack();

                    summary.FailedCount++;
                    ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: {ex.Message}");
                }

                processed++;
                ViewModel.ReportProgress(processed);

                System.Windows.Application.Current?.Dispatcher.Invoke(
                    DispatcherPriority.Background, new Action(() => { }));
            }

            var status = tx.Commit();
            if (status != TransactionStatus.Committed)
            {
                ViewModel.AddLog(LogLevel.Error, $"Transaction did not commit cleanly: {status}");
                ViewModel.ShowCriticalError(
                    $"The transaction ended with status '{status}' instead of committing. " +
                    "Check the model for overlapping or invalid geometry and try again.");
            }

            ViewModel.OnRunComplete(request.Mode, summary, wasCancelled);
        }

        public string GetName() => "Floors and Roofs From Linked Rooms — run handler";
    }
}
