using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Models;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Engine
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
            Func<bool> cancellationRequested)
        {
            var summary = new RunSummary();

            // Host roofs used as anchors for wall–roof corner resolution. These are
            // ALWAYS host-owned (roofs live in the host per the confirmed split), so
            // they are already in host coordinates. Not filtered by IsSelected on
            // purpose: any roof in scope is a valid corner anchor for a wall.
            var hostRoofs = selectedTypes
                .Where(t => t.Bic == BuiltInCategory.OST_Roofs)
                .SelectMany(t => t.ElementIds.Select(id => t.SourceDocument.GetElement(id)))
                .Where(e => e != null)
                .ToList();

            // Flatten selected instances, keeping each row's source doc + transform.
            var work = selectedTypes
                .Where(t => t.IsSelected)
                .SelectMany(t => t.ElementIds.Select(id => (Row: t, ElementId: id)))
                .ToList();

            int total = work.Count, current = 0;

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

                    var origin = _originService.GetOrigin(element, row.Bic, row.LinkTransform, hostRoofs);
                    if (!origin.IsValid)
                    {
                        summary.SkippedCount++;
                        Log(LogLevel.Warning, $"Skip · {row.Category} · id {elementId.Value} · {origin.SkipReason}");
                        continue;
                    }

                    XYZ finalOrigin = origin.Origin;
                    if (row.Bic == BuiltInCategory.OST_Walls)
                    {
                        var resolved = _clashService.ResolveClashFreeOrigin(origin.Origin);
                        if (resolved == null)
                        {
                            summary.SkippedCount++;
                            Log(LogLevel.Warning, $"Skip · Walls · id {elementId.Value} · no clash-free position.");
                            continue;
                        }
                        finalOrigin = resolved;
                    }

                    using (var sub = new SubTransaction(_hostDoc))
                    {
                        sub.Start();
                        var result = _placementService.PlaceReferenceSection(
                            activeViewId, finalOrigin, origin.ViewDirection,
                            mapping.MappedDraftingView.RevitView.Id, sectionTypeId);

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
