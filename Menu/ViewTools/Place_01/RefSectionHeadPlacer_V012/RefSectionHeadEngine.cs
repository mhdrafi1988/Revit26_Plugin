using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RefSectionHeadPlacer.V012.Core.Models;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RefSectionHeadPlacer.V012.Core.Engine
{
    public class RunSummary
    {
        public int PlacedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }

    /// <summary>
    /// Orchestrates a run: for every selected type row, for every instance,
    /// resolve a HOST-space origin (link elements transformed via the row's
    /// LinkTransform inside SectionOriginService), clash-avoid walls, then place
    /// a reference section head in the active host view.
    ///
    /// TRANSACTION MODEL (corrected): a SubTransaction REQUIRES an open host
    /// Transaction — a TransactionGroup is not a Transaction. So we open ONE
    /// Transaction for the whole run and isolate each item in its own
    /// SubTransaction. One item's failure rolls back only that SubTransaction;
    /// the run continues. The whole run is a single undo step.
    /// </summary>
    public class RefSectionHeadEngine
    {
        private readonly Document _hostDoc;
        private readonly Services.SectionOriginService _originService;
        private readonly Services.ClashAvoidanceService _clashService;
        private readonly Services.SectionPlacementService _placementService;

        public event Action<LogEntry> LogEmitted;
        public event Action<int, int> ProgressChanged;

        public RefSectionHeadEngine(
            Document hostDoc,
            Services.SectionOriginService originService,
            Services.ClashAvoidanceService clashService,
            Services.SectionPlacementService placementService)
        {
            _hostDoc = hostDoc;
            _originService = originService;
            _clashService = clashService;
            _placementService = placementService;
        }

        public RunSummary Run(
            IReadOnlyList<ElementTypeRow> selectedTypes,
            IReadOnlyList<CategoryMappingRow> mappings,
            ElementId activeViewId,
            ElementId sectionTypeId,
            double tailLengthFeet,
            Func<bool> cancellationRequested)
        {
            var summary = new RunSummary();

            // V012: the hostRoofs collection is GONE — it fed only the deleted
            // wall–roof-corner heuristic (SectionOriginService.WallCorner). Wall
            // marks now sit on the wall's own location line.

            // Flatten selected instances, keeping each row's source doc + transform.
            var work = selectedTypes
                .Where(t => t.IsSelected)
                .SelectMany(t => t.ElementIds.Select(id => (Row: t, ElementId: id)))
                .ToList();

            int total = work.Count, current = 0;
            Log(LogLevel.Info, $"Starting run · {total} item(s) across {selectedTypes.Count(t => t.IsSelected)} selected type(s).");

            using (var tx = new Transaction(_hostDoc, "Place Reference Section Heads"))
            {
                tx.Start();

                foreach (var (row, elementId) in work)
                {
                    current++;
                    ProgressChanged?.Invoke(current, total);

                    // Cancel is polled between items only — an in-progress
                    // SubTransaction is never aborted mid-way.
                    if (cancellationRequested?.Invoke() == true)
                    {
                        Log(LogLevel.Warning, $"Run cancelled at item {current} of {total}.");
                        break;
                    }

                    // Linked ElementIds must be resolved via their OWN document.
                    var element = row.SourceDocument.GetElement(elementId);
                    if (element == null)
                    {
                        summary.SkippedCount++;
                        Log(LogLevel.Warning, $"Skip · {row.Category} · id {elementId.Value} · element not found.");
                        continue;
                    }

                    // Exact (SourceLabel, Category, TypeName) mapping — distinguishes
                    // drain vs wash basin AND keeps two links' matching types separate.
                    var mapping = mappings.FirstOrDefault(m =>
                        m.IsSelected && m.SourceLabel == row.SourceLabel &&
                        m.Bic == row.Bic && m.TypeName == row.TypeName);
                    if (mapping?.MappedDraftingView == null)
                    {
                        summary.SkippedCount++;
                        Log(LogLevel.Warning, $"Skip · {row.SourceLabel} · {row.Category} · {row.TypeName} · no drafting-view mapping.");
                        continue;
                    }

                    var origin = _originService.GetOrigin(element, row.Bic, row.LinkTransform, tailLengthFeet);
                    if (!origin.IsValid)
                    {
                        summary.SkippedCount++;
                        Log(LogLevel.Warning, $"Skip · {row.Category} · id {elementId.Value} · {origin.SkipReason}");
                        continue;
                    }

                    // V012 (confirmed, Rafi): one mark per wall, first clash-free
                    // spot on the wall line — mid → ¼ → ¾. ASSUMPTION (flagged in
                    // plan, accepted): if all three on-wall candidates clash, fall
                    // back to the pre-existing radial nudge around the midpoint
                    // before skipping with a Warning.
                    XYZ finalOrigin = origin.Origin;
                    if (row.Bic == BuiltInCategory.OST_Walls)
                    {
                        finalOrigin = null;
                        var candidates = origin.CandidateOrigins ?? new[] { origin.Origin };

                        foreach (var candidate in candidates)
                        {
                            if (_clashService.IsClear(candidate)) { finalOrigin = candidate; break; }
                        }

                        if (finalOrigin == null)
                        {
                            finalOrigin = _clashService.ResolveClashFreeOrigin(origin.Origin);
                            if (finalOrigin != null)
                                Log(LogLevel.Warning, $"Walls · id {elementId.Value} · mid/¼/¾ all occupied — nudged off the wall line to avoid overlap.");
                        }

                        if (finalOrigin == null)
                        {
                            summary.SkippedCount++;
                            Log(LogLevel.Warning, $"Skip · Walls · id {elementId.Value} · no clash-free position (mid, ¼, ¾ and radial nudge all occupied).");
                            continue;
                        }
                    }

                    using (var sub = new SubTransaction(_hostDoc))
                    {
                        sub.Start();
                        var result = _placementService.PlaceReferenceSection(
                            activeViewId, finalOrigin, origin.ViewDirection,
                            mapping.MappedDraftingView.RevitView.Id, sectionTypeId, tailLengthFeet);

                        if (result.Success && sub.Commit() == TransactionStatus.Committed)
                        {
                            summary.PlacedCount++;
                            if (row.Bic == BuiltInCategory.OST_Walls) _clashService.RegisterPlacement(finalOrigin);
                            Log(LogLevel.Success, $"Placed · {row.Category} · {row.TypeName} → {mapping.MappedDraftingView.Name}");
                        }
                        else
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            summary.FailedCount++;
                            Log(LogLevel.Error, $"Failed · {row.Category} · id {elementId.Value} · {result.Message ?? "sub-transaction not committed"}");
                        }
                    }
                }

                tx.Commit();
            }

            Log(LogLevel.Info, $"Run complete — {summary.PlacedCount} placed | {summary.SkippedCount} skipped | {summary.FailedCount} failed.");
            return summary;
        }

        private void Log(LogLevel level, string message) => LogEmitted?.Invoke(new LogEntry(level, message));
    }
}
