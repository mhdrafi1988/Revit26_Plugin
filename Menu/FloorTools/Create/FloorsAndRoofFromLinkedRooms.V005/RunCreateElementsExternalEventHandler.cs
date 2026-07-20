using System;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005
{
    /// <summary>Runs either a floor-creation pass or a roof-creation pass, depending on
    /// which button the ViewModel set on CreateRunRequest.Mode. Each room targets its own
    /// mapped host Level (RoomCandidate.SelectedHostLevel).
    ///
    /// V005 changes:
    /// - Both doc.Regenerate() calls removed (Revit regenerates on commit; ~2x faster runs).
    /// - FIX: processed count now advances on EVERY room outcome (success, failure,
    ///   exception) — V004's `continue` on invalid boundaries skipped ReportProgress,
    ///   so the progress bar undercounted on failed rooms.
    /// - FIX: roof inner-loop count now combines BOTH skip sources — valid inner loops
    ///   (unsupported by NewFootPrintRoof) AND loops that failed validation. V004's
    ///   if/else counted only one of the two per room.
    /// - Cancel now records how many rooms were never reached (RunSummary.NotProcessedCount);
    ///   already-created elements are kept, per confirmed spec.
    /// - V005 addition: RoofCreationService.Create now returns RoofCreationResult instead
    ///   of FootPrintRoof directly. When UsedFallback is true (real room boundary failed,
    ///   4m x 4m placeholder square created at host origin instead), this is logged as a
    ///   Warning and tallied into summary.FailedCount — NOT summary.SuccessCount — since
    ///   the room's actual geometry was not used. Floors are unaffected (no fallback
    ///   behavior added to FloorCreationService).
    /// - V005 REFACTOR: roof creation no longer runs inside a per-room SubTransaction.
    ///   NewFootPrintRoof was throwing "Value cannot be null" on every attempt regardless
    ///   of curve validity, level, or roof type — confirmed via full diagnostic trace
    ///   that ruled out geometry/level/type as causes. Roof creation now calls
    ///   RoofCreationService.Create() directly against the outer Transaction, with no
    ///   SubTransaction wrapping it, to test/resolve a possible transaction-nesting
    ///   cause. TRADEOFF (confirmed by Rafi): roof creation loses per-room rollback
    ///   isolation — a room-level exception is caught and the room is skipped/counted
    ///   as failed, but there is no SubTransaction to roll back for that room
    ///   specifically. Floor creation is completely unaffected and still runs inside
    ///   its own per-room SubTransaction exactly as before.</summary>
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

            var summary = new RunSummary { UnmappedSkippedCount = UnmappedSkippedCount };
            bool wasCancelled = false;
            int total = request.Rooms.Count;
            int processed = 0;

            ViewModel.AddLog(LogLevel.Info,
                $"Run started — mode: {request.Mode}, {total} mapped room(s), type id {request.TypeId.Value}.");

            using var tx = new Transaction(doc, isRoof ? "Create roofs from linked rooms" : "Create floors from linked rooms");
            var failureOptions = tx.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new FloorFailuresPreprocessor());
            tx.SetFailureHandlingOptions(failureOptions);
            tx.Start();

            foreach (var candidate in request.Rooms)
            {
                if (request.Cancel != null && request.Cancel.IsCancelled)
                {
                    wasCancelled = true;
                    summary.NotProcessedCount = total - processed;
                    ViewModel.AddLog(LogLevel.Warning,
                        $"Run cancelled by user — {summary.NotProcessedCount} remaining room(s) not processed; " +
                        "already-created elements are kept.");
                    break;
                }

                if (isRoof)
                {
                    // V005 refactor: roof creation no longer runs inside a per-room
                    // SubTransaction. NewFootPrintRoof was throwing an unhelpful
                    // "Value cannot be null" exception on every attempt regardless of
                    // curve validity, level, or roof type (confirmed via full diagnostic
                    // trace) — isolating to the ExternalEvent/transaction-nesting context
                    // as the remaining candidate. This removes the SubTransaction layer
                    // specifically around roof creation to test/resolve that.
                    //
                    // TRADEOFF (flagged per Rafi's confirmation): roof creation no longer
                    // has per-room rollback isolation. If a room's roof creation throws,
                    // there is no SubTransaction to roll back — only this room's own
                    // partial state (if any) is at risk, and the room is still counted
                    // as failed and skipped; the outer Transaction is not rolled back
                    // here. Floors are completely unaffected — FloorCreationService still
                    // runs inside its own SubTransaction exactly as before.
                    try
                    {
                        if (candidate.SelectedHostLevel?.LevelElement == null)
                        {
                            summary.FailedCount++;
                            ViewModel.AddLog(LogLevel.Warning,
                                $"{candidate.DisplayName} — skipped unexpectedly: no New Level mapped (should have been filtered before run).");
                        }
                        else
                        {
                            var targetLevel = candidate.SelectedHostLevel.LevelElement;
                            var boundary = RoomBoundaryService.BuildLoops(candidate.RoomElement, request.LinkTransform);

                            if (!boundary.OuterValid)
                            {
                                summary.FailedCount++;
                                ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: {boundary.FailureReason}");
                            }
                            else
                            {
                                var roofResult = RoofCreationService.Create(doc, boundary.Loops[0], request.TypeId, targetLevel);

                                // Full diagnostic trace, always logged at Info level regardless
                                // of outcome — every check, input value, and API call result
                                // recorded during RoofCreationService.Create() for this room.
                                if (roofResult.Diagnostics != null)
                                {
                                    ViewModel.AddLog(LogLevel.Info,
                                        $"{candidate.DisplayName} — roof creation diagnostics:");
                                    foreach (var line in roofResult.Diagnostics)
                                        ViewModel.AddLog(LogLevel.Info, $"    {line}");
                                }

                                if (roofResult.UsedFallback)
                                {
                                    // Real room boundary failed roof creation; a fixed 4m x 4m
                                    // placeholder square at host origin (0,0) was created instead.
                                    // This counts as a FAILURE, not a success — the room's actual
                                    // geometry was not used and needs manual attention.
                                    summary.FailedCount++;
                                    ViewModel.AddLog(LogLevel.Warning,
                                        $"{candidate.DisplayName} — roof creation failed on real boundary, " +
                                        $"4m x 4m fallback placeholder roof created at origin (0,0) on '{targetLevel.Name}' instead. " +
                                        $"Original failure: {roofResult.OriginalFailureReason}");
                                }
                                else
                                {
                                    summary.SuccessCount++;

                                    if (boundary.WasTrimmedOrFixed)
                                    {
                                        summary.TrimmedFixedCount++;
                                        ViewModel.AddLog(LogLevel.Warning,
                                            $"{candidate.DisplayName} — roof created on '{targetLevel.Name}' (boundary trimmed/fixed)");
                                    }
                                    else
                                    {
                                        ViewModel.AddLog(LogLevel.Success,
                                            $"{candidate.DisplayName} — roof created on '{targetLevel.Name}'");
                                    }
                                }

                                // Inner-loop accounting (V005 fix): valid inner loops
                                // (unsupported by NewFootPrintRoof) AND loops that failed
                                // validation are both counted as skipped.
                                int skippedInner = (boundary.Loops.Count - 1) + boundary.InnerLoopsSkipped;
                                if (skippedInner > 0)
                                {
                                    summary.InnerLoopsSkippedCount += skippedInner;
                                    ViewModel.AddLog(LogLevel.Warning,
                                        $"{candidate.DisplayName} — {skippedInner} inner loop(s) not supported for roofs, outer boundary used");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        summary.FailedCount++;
                        ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: {ex.Message}");

                        // If RoofCreationService attached a full diagnostic trace to the
                        // exception (both real-curve and fallback attempts failed), surface
                        // it in full at Info level so the root cause can be diagnosed.
                        if (ex.Data.Contains("Diagnostics") && ex.Data["Diagnostics"] is System.Collections.Generic.List<string> diagLines)
                        {
                            ViewModel.AddLog(LogLevel.Info,
                                $"{candidate.DisplayName} — roof creation diagnostics (both attempts failed):");
                            foreach (var line in diagLines)
                                ViewModel.AddLog(LogLevel.Info, $"    {line}");
                        }
                    }
                }
                else
                {
                    // Floor path — unchanged. Still runs inside its own per-room
                    // SubTransaction for isolated rollback on failure.
                    using (var subTx = new SubTransaction(doc))
                    {
                        subTx.Start();
                        try
                        {
                            if (candidate.SelectedHostLevel?.LevelElement == null)
                            {
                                subTx.RollBack();
                                summary.FailedCount++;
                                ViewModel.AddLog(LogLevel.Warning,
                                    $"{candidate.DisplayName} — skipped unexpectedly: no New Level mapped (should have been filtered before run).");
                            }
                            else
                            {
                                var targetLevel = candidate.SelectedHostLevel.LevelElement;
                                var boundary = RoomBoundaryService.BuildLoops(candidate.RoomElement, request.LinkTransform);

                                if (!boundary.OuterValid)
                                {
                                    subTx.RollBack();
                                    summary.FailedCount++;
                                    ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: {boundary.FailureReason}");
                                }
                                else
                                {
                                    FloorCreationService.Create(doc, boundary.Loops, request.TypeId, targetLevel);
                                    subTx.Commit();
                                    summary.SuccessCount++;

                                    if (boundary.WasTrimmedOrFixed)
                                    {
                                        summary.TrimmedFixedCount++;
                                        ViewModel.AddLog(LogLevel.Warning,
                                            $"{candidate.DisplayName} — floor created on '{targetLevel.Name}' (boundary trimmed/fixed)");
                                    }
                                    else
                                    {
                                        ViewModel.AddLog(LogLevel.Success,
                                            $"{candidate.DisplayName} — floor created on '{targetLevel.Name}'");
                                    }

                                    // Floors keep all inner loops (holes) — only
                                    // validation-failed ones are skipped.
                                    if (boundary.InnerLoopsSkipped > 0)
                                    {
                                        summary.InnerLoopsSkippedCount += boundary.InnerLoopsSkipped;
                                        ViewModel.AddLog(LogLevel.Warning,
                                            $"{candidate.DisplayName} — {boundary.InnerLoopsSkipped} inner loop(s) skipped, outer boundary used");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (subTx.GetStatus() == TransactionStatus.Started)
                                subTx.RollBack();

                            summary.FailedCount++;
                            ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: {ex.Message}");
                        }
                    }
                }

                // V005 fix: every room outcome advances the progress count — no `continue`
                // paths can bypass this anymore.
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
