using System;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.FloorsFromLinkedRoomsViaPlanView.V001
{
    public class RunFloorsExternalEventHandler : IExternalEventHandler
    {
        public MainViewModel ViewModel { get; set; }
        public FloorRunRequest PendingRequest { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var request = PendingRequest;
            if (request == null) return;

            var summary = new RunSummary();
            bool wasCancelled = false;

            using var tx = new Transaction(doc, "Create floors from linked rooms");
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
                    FloorCreationService.Create(doc, boundary.Loops, request.FloorTypeId, request.TargetLevel);
                    doc.Regenerate();

                    subTx.Commit();
                    summary.SuccessCount++;

                    if (boundary.WasTrimmedOrFixed)
                    {
                        summary.TrimmedFixedCount++;
                        ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — floor created (boundary trimmed/fixed)");
                    }
                    else
                    {
                        ViewModel.AddLog(LogLevel.Success, $"{candidate.DisplayName} — floor created");
                    }

                    if (boundary.InnerLoopsSkipped > 0)
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

                // Pumps the dispatcher so the progress bar / status text actually repaint
                // between rooms — Execute() runs synchronously on the UI thread otherwise,
                // and WPF won't render until it returns. Standard, if slightly hacky, fix.
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

            ViewModel.OnRunComplete(summary, wasCancelled);
        }

        public string GetName() => "Floors From Linked Rooms — run handler";
    }
}
