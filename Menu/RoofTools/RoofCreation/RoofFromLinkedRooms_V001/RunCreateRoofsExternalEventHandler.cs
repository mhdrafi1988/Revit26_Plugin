using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.RoofFromLinkedRooms.V001
{
    /// <summary>
    /// Creates one flat roof per selected, level-mapped linked room inside a single
    /// Transaction. Each roof runs in its own SubTransaction, so one failure does not
    /// roll back roofs already created in the same run.
    /// </summary>
    public class RunCreateRoofsExternalEventHandler : IExternalEventHandler
    {
        /// <summary>The window's view model; receives logs and the final summary.</summary>
        public MainViewModel ViewModel { get; set; }

        /// <summary>The queued run; consumed by <see cref="Execute"/>.</summary>
        public CreateRoofsRunRequest PendingRequest { get; set; }

        /// <summary>Revit entry point — guarded so no exception reaches Revit.</summary>
        public void Execute(UIApplication app)
            => ToolGuard.RunHandler(GetType(), app, ExecuteUnguarded);

        private void ExecuteUnguarded(UIApplication app)
        {
            var request = PendingRequest;
            PendingRequest = null;
            if (request == null) return;

            var doc = app.ActiveUIDocument.Document;
            var summary = new RunSummary();
            var createdIds = new List<ElementId>();
            var withOpeningsIds = new List<ElementId>();

            if (doc.IsReadOnly)
            {
                ViewModel.AddLog(LogLevel.Error, "The host document is read-only — no roofs were created.");
                ViewModel.OnRunComplete(summary, createdIds, withOpeningsIds);
                return;
            }

            ViewModel.AddLog(LogLevel.Info, $"Run started — {request.Rooms.Count} room(s).");

            using var tx = new Transaction(doc, "Create roofs from linked rooms");
            tx.Start();

            foreach (var candidate in request.Rooms)
            {
                using var subTx = new SubTransaction(doc);
                subTx.Start();
                try
                {
                    var level = candidate.SelectedHostLevel?.LevelElement;
                    if (level == null)
                    {
                        subTx.RollBack();
                        summary.UnmappedSkippedCount++;
                        SetStatus(candidate, "Unmapped", "Unmapped", "—");
                        ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: no host level matched.");
                        continue;
                    }

                    var boundary = RoomBoundaryService.BuildLoops(candidate.RoomElement, request.LinkTransform);
                    if (!boundary.OuterValid)
                    {
                        subTx.RollBack();
                        summary.FailedCount++;
                        SetStatus(candidate, "Failed", "Invalid boundary", "—");
                        ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — skipped: {boundary.FailureReason}");
                        continue;
                    }

                    var roofId = RoofCreationService.Create(
                        doc, boundary.Loops, request.RoofTypeId, level, ViewModel.AddLog, out int openings);
                    subTx.Commit();

                    summary.RoofsCreatedCount++;
                    summary.OpeningsCutCount += openings;
                    createdIds.Add(roofId);
                    if (openings > 0) withOpeningsIds.Add(roofId);
                    if (boundary.WasTrimmedOrFixed) summary.TrimmedFixedCount++;

                    SetStatus(candidate, "Created", boundary.WasTrimmedOrFixed ? "Created (trimmed)" : "Created", openings.ToString());
                    ViewModel.AddLog(boundary.WasTrimmedOrFixed ? LogLevel.Warning : LogLevel.Success,
                        $"{candidate.DisplayName} — roof {roofId.Value} created on '{level.Name}'" +
                        (openings > 0 ? $", {openings} opening(s)" : "") +
                        (boundary.WasTrimmedOrFixed ? " (boundary trimmed/fixed)" : "") + ".");
                    if (boundary.InnerLoopsSkipped > 0)
                        ViewModel.AddLog(LogLevel.Warning,
                            $"{candidate.DisplayName} — {boundary.InnerLoopsSkipped} invalid inner loop(s) skipped.");
                }
                catch (Exception ex)
                {
                    if (subTx.GetStatus() == TransactionStatus.Started)
                        subTx.RollBack();

                    summary.FailedCount++;
                    SetStatus(candidate, "Failed", "Failed", "—");
                    ViewModel.AddLog(LogLevel.Warning, $"{candidate.DisplayName} — failed: {ex.Message}");
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

            ViewModel.OnRunComplete(summary, createdIds, withOpeningsIds);
        }

        private static void SetStatus(RoomCandidate c, string kind, string label, string openings)
        {
            c.StatusKind = kind;
            c.StatusLabel = label;
            c.OpeningsText = openings;
        }

        /// <summary>Name shown by Revit for this handler.</summary>
        public string GetName() => "Roof From Linked Rooms — run handler";
    }
}
