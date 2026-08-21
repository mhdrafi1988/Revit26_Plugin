using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromDetailLines.V007
{
    /// <summary>
    /// Runs the roof-creation pass for every RoofGroup in the request, inside a single
    /// Transaction (one logical operation, per the suite's transaction standard).
    /// Each roof is created inside a SubTransaction so one failure doesn't roll back
    /// roofs already created earlier in the same run.
    ///
    /// Deletion of source detail lines (when opted in) happens AFTER all roofs are
    /// created, and only for loops that were NOT auto-closed — auto-closed loops'
    /// original lines are always kept, per confirmed spec.
    /// </summary>
    public class RunCreateRoofsExternalEventHandler : IExternalEventHandler
    {
        public MainViewModel ViewModel { get; set; }
        public CreateRoofsRunRequest PendingRequest { get; set; }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;
            var request = PendingRequest;
            if (request == null) return;

            var summary = new RunSummary();
            var createdRoofIds = new List<ElementId>();
            var autoClosedRoofIds = new List<ElementId>();
            var roofsWithOpeningsIds = new List<ElementId>();

            ViewModel.AddLog(LogLevel.Info,
                $"Run started — {request.RoofGroups.Count} roof group(s) targeting level '{request.Level.Name}', " +
                $"delete source lines: {request.DeleteSourceLines}.");

            using var tx = new Transaction(doc, "Create roofs from detail lines");
            tx.Start();

            foreach (var group in request.RoofGroups)
            {
                using var subTx = new SubTransaction(doc);
                subTx.Start();
                try
                {
                    var roofId = RoofCreationService.Create(doc, group, request.RoofTypeId, request.Level, ViewModel.AddLog);
                    subTx.Commit();

                    summary.RoofsCreatedCount++;
                    createdRoofIds.Add(roofId);

                    if (group.ContainsAutoClosed)
                    {
                        autoClosedRoofIds.Add(roofId);
                        summary.AutoClosedLoopCount++;
                    }
                    if (group.ContainsCornerTrimmed)
                        summary.CornerTrimmedLoopCount++;
                    if (group.Openings.Count > 0)
                        roofsWithOpeningsIds.Add(roofId);

                    ViewModel.AddLog(LogLevel.Success,
                        $"Roof {group.RoofNumber} created (id {roofId.Value}) on '{request.Level.Name}'" +
                        (group.Openings.Count > 0 ? $", {group.Openings.Count} opening(s)" : "") + ".");
                }
                catch (Exception ex)
                {
                    if (subTx.GetStatus() == TransactionStatus.Started)
                        subTx.RollBack();

                    summary.FailedCount++;
                    ViewModel.AddLog(LogLevel.Warning, $"Roof {group.RoofNumber} — failed: {ex.Message}");
                }
            }

            // ── Optional deletion of source lines (closed + corner-trimmed loops only) ──
            if (request.DeleteSourceLines)
            {
                var idsToDelete = new HashSet<ElementId>();
                foreach (var group in request.RoofGroups)
                {
                    CollectDeletableLineIds(group.Outer, idsToDelete);
                    foreach (var opening in group.Openings)
                        CollectDeletableLineIds(opening, idsToDelete);
                }

                if (idsToDelete.Count > 0)
                {
                    try
                    {
                        doc.Delete(idsToDelete);
                        summary.SourceLinesDeletedCount = idsToDelete.Count;
                        ViewModel.AddLog(LogLevel.Info,
                            $"Deleted {idsToDelete.Count} source detail line(s) from closed/corner-trimmed loops (auto-closed loop lines were kept).");
                    }
                    catch (Exception ex)
                    {
                        ViewModel.AddLog(LogLevel.Warning, $"Could not delete source lines: {ex.Message}");
                    }
                }
            }

            var status = tx.Commit();
            if (status != TransactionStatus.Committed)
            {
                ViewModel.AddLog(LogLevel.Error, $"Transaction did not commit cleanly: {status}");
                ViewModel.ShowCriticalError(
                    $"The transaction ended with status '{status}' instead of committing. " +
                    "Check the model for overlapping or invalid geometry and try again.");
            }

            ViewModel.OnRunComplete(summary, createdRoofIds, autoClosedRoofIds, roofsWithOpeningsIds);
        }

        private static void CollectDeletableLineIds(LoopCandidate loop, HashSet<ElementId> idsToDelete)
        {
            // Auto-closed loops keep ALL their original source lines, per confirmed spec.
            if (loop.WasAutoClosed) return;

            foreach (var id in loop.SourceLineIds)
                idsToDelete.Add(id);
        }

        public string GetName() => "Roof From Detail Lines — run handler";
    }
}
