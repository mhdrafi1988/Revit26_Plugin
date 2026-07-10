using System;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V003
{
    /// <summary>Runs either a floor-creation pass or a roof-creation pass, depending on
    /// which button the ViewModel set on CreateRunRequest.Mode. Same per-room
    /// dedupe/trim/validate boundary logic feeds both.</summary>
    public class RunCreateElementsExternalEventHandler : IExternalEventHandler
    {
        public MainViewModel ViewModel { get; set; }
        public CreateRunRequest PendingRequest { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var request = PendingRequest;
            if (request == null) return;

            bool isRoof = request.Mode == CreationMode.Roof;
            string verb = isRoof ? "roof" : "floor";

            var summary = new RunSummary();
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

                    RoofCreationTier? roofTier = null;
                    if (isRoof)
                    {
                        var rawLoop = RoofCreationService.BuildRawLoop(candidate.RoomElement, request.LinkTransform);
                        var checklist = RoofCreationService.DescribeInputs(doc, candidate.RoomElement, request.LinkTransform,
                            rawLoop, boundary.Loops[0], request.TypeId, request.TargetLevel);
                        ViewModel.AddLog(LogLevel.Info, $"{candidate.DisplayName} — roof input checklist:");
                        foreach (var line in checklist)
                            ViewModel.AddLog(LogLevel.Info, "    " + line);

                        var roofResult = RoofCreationService.Create(doc, candidate.RoomElement, request.LinkTransform,
                            boundary.Loops[0], request.TypeId, request.TargetLevel);
                        roofTier = roofResult.Tier;
                    }
                    else
                    {
                        FloorCreationService.Create(doc, boundary.Loops, request.TypeId, request.TargetLevel);
                    }

                    doc.Regenerate();

                    subTx.Commit();
                    summary.SuccessCount++;

                    bool trimmedOrFallback = boundary.WasTrimmedOrFixed
                        || roofTier is RoofCreationTier.ZFlattened or RoofCreationTier.ReversedWinding;

                    if (roofTier == RoofCreationTier.BoundingBoxApproximate)
                    {
                        // Always called out distinctly — this changes the roof's actual shape,
                        // it is not a normal trim/fix, and must never be silently folded in.
                        summary.TrimmedFixedCount++;
                        ViewModel.AddLog(LogLevel.Warning,
                            $"{candidate.DisplayName} — roof created from an APPROXIMATE bounding-box outline (all other creation attempts failed) — shape does not match the actual room");
                    }
                    else if (trimmedOrFallback)
                    {
                        summary.TrimmedFixedCount++;
                        string tierNote = roofTier switch
                        {
                            RoofCreationTier.ZFlattened => " (created via Z-flatten retry)",
                            RoofCreationTier.ReversedWinding => " (created via reversed-winding retry)",
                            _ => " (boundary trimmed/fixed)"
                        };
                        ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — {verb} created{tierNote}");
                    }
                    else
                    {
                        ViewModel.AddLog(LogLevel.Success, $"{candidate.DisplayName} — {verb} created");
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
                    ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: {ExceptionFormatting.Full(ex)}");
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
