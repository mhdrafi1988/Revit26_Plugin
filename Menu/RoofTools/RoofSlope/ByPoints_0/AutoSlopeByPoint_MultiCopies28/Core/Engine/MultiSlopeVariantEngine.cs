// =======================================================
// File: MultiSlopeVariantEngine.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.MultiCopies28
// Purpose: Orchestrates the Multi-Slope Roof Variants feature.
//
// Transaction structure (confirmed, follow exactly):
//   - ONE outer Transaction per roof.
//   - Inside it, ONE SubTransaction per active slope row: copy the
//     source roof (drain XY preserved via zero-translation copy),
//     apply that row's slope, resolve/create its workset, assign
//     ELEM_PARTITION_PARAM, then commit that SubTransaction.
//   - If a slope's SubTransaction fails, it is rolled back and logged
//     as an error; already-committed slopes for this same roof are
//     NOT rolled back — the loop continues to the next slope.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Core.Engine
{
    public static class MultiSlopeVariantEngine
    {
        public static void Execute(UIApplication app, MultiSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;

            RoofBase sourceRoof = doc.GetElement(data.RoofId) as RoofBase;
            if (sourceRoof == null)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error, "Source roof element not found. Aborting."));
                Abort(data, "Source roof element not found. Aborting.");
                return;
            }

            if (!doc.IsWorkshared)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "This document is not workshared — Multi-Slope Roof Variants requires worksets. Aborting."));
                Abort(data, "Document is not workshared.");
                return;
            }

            List<SlopeRowSetting> activeRows = (data.SlopeRows ?? new List<SlopeRowSetting>())
                .Where(r => r.IsEnabled && r.Percent > 0)
                .OrderBy(r => r.RowIndex)
                .ToList();

            if (activeRows.Count == 0)
            {
                data.Log?.Invoke(new LogEntry(LogLevel.Error,
                    "No active slope rows selected. Aborting."));
                Abort(data, "No active slope rows selected.");
                return;
            }

            data.Log?.Invoke(new LogEntry(LogLevel.Info,
                $"Starting Multi-Slope Roof Variants — source roof {sourceRoof.Id}, {activeRows.Count} active slope(s)."));

            var results = new List<MultiSlopeVariantResult>();

            using (Transaction tx = new Transaction(doc, $"Multi-Slope Roof Variants — Roof {sourceRoof.Id}"))
            {
                tx.Start();

                foreach (SlopeRowSetting row in activeRows)
                {
                    MultiSlopeVariantResult rowResult = ProcessRow(app, doc, sourceRoof, data, row);
                    results.Add(rowResult);
                }

                tx.Commit();
            }

            int copiesCreated = results.Count(r => r.Success);
            int worksetsCreated = results.Count(r => r.Success && r.WorksetWasCreated);
            int failed = results.Count(r => !r.Success);

            data.Log?.Invoke(new LogEntry(
                failed == 0 ? LogLevel.Success : LogLevel.Warning,
                $"{copiesCreated} copies created | {worksetsCreated} worksets created | {failed} failed"));

            data.OnCompleted?.Invoke(new MultiSlopeRunSummary
            {
                Success = true,
                Results = results,
                CopiesCreated = copiesCreated,
                WorksetsCreated = worksetsCreated,
                Failed = failed
            });
        }

        private static MultiSlopeVariantResult ProcessRow(
            UIApplication app, Document doc, RoofBase sourceRoof, MultiSlopePayload data, SlopeRowSetting row)
        {
            var result = new MultiSlopeVariantResult
            {
                RowIndex = row.RowIndex,
                Percent = row.Percent
            };

            using (SubTransaction sub = new SubTransaction(doc))
            {
                sub.Start();

                try
                {
                    // ── Copy the roof — zero translation keeps footprint and
                    // drain point XY identical to the source; only slope/Z changes.
                    ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElement(
                        doc, sourceRoof.Id, XYZ.Zero);

                    ElementId copyId = copiedIds.FirstOrDefault();
                    if (copyId == null || copyId == ElementId.InvalidElementId)
                        throw new InvalidOperationException("CopyElement returned no new element.");

                    RoofBase roofCopy = doc.GetElement(copyId) as RoofBase;
                    if (roofCopy == null)
                        throw new InvalidOperationException("Copied element is not a roof.");

                    result.CopyRoofId = copyId;
                    data.Log?.Invoke(new LogEntry(LogLevel.Info,
                        $"Slope {row.Percent}%: copied roof {sourceRoof.Id} → {copyId}."));

                    // ── Apply this row's slope to the copy ──────────────────
                    var slopePayload = new AutoSlopePayload
                    {
                        RoofId = copyId,
                        PickedDrainPoints = data.PickedDrainPoints,
                        DrainPoints = data.DrainPoints,
                        SlopePercent = row.Percent,
                        ThresholdMeters = data.ThresholdMeters,
                        EnableDrainTolerance = data.EnableDrainTolerance,
                        DrainToleranceMm = data.DrainToleranceMm,
                        InsertCurveIntersectionPoints = data.InsertCurveIntersectionPoints,
                        ExportConfig = data.ExportConfig,
                        ProjectTitle = data.ProjectTitle,
                        DrainMarkerGroup = data.DrainMarkerGroup,
                        HighestPointMarkerGroup = data.HighestPointMarkerGroup,
                        AllowedOffsetMarkerGroup = data.AllowedOffsetMarkerGroup,
                        AllowedOffsetThresholdMm = data.AllowedOffsetThresholdMm,
                        Log = data.Log
                    };

                    AutoSlopeResult engineResult = AutoSlopeEngine.ApplySlope(app, slopePayload);
                    result.EngineResult = engineResult;

                    if (!engineResult.Success)
                        throw new InvalidOperationException(engineResult.ErrorMessage ?? "Slope apply failed.");

                    // ── Resolve/create workset and assign the copy to it ────
                    string worksetName = string.Format(data.WorksetNameFormat, FormatPercent(row.Percent));
                    WorksetResolver.Result ws = WorksetResolver.Resolve(doc, worksetName);

                    Parameter partitionParam = roofCopy.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (partitionParam == null || partitionParam.IsReadOnly)
                        throw new InvalidOperationException("Could not set workset on roof copy (ELEM_PARTITION_PARAM unavailable).");

                    partitionParam.Set(ws.WorksetId.IntegerValue);

                    result.WorksetName = worksetName;
                    result.WorksetWasCreated = ws.WasCreated;
                    result.Success = true;

                    data.Log?.Invoke(new LogEntry(LogLevel.Success,
                        $"Slope {row.Percent}%: workset '{worksetName}' " +
                        $"({(ws.WasCreated ? "newly created" : "existing")}) — roof {copyId} committed."));

                    sub.Commit();
                }
                catch (Exception ex)
                {
                    sub.RollBack();

                    result.Success = false;
                    result.ErrorMessage = ex.Message;

                    data.Log?.Invoke(new LogEntry(LogLevel.Error,
                        $"Slope {row.Percent}% (row {row.RowIndex}) failed: {ex.Message}"));
                }
            }

            return result;
        }

        private static void Abort(MultiSlopePayload data, string reason)
        {
            data.OnCompleted?.Invoke(new MultiSlopeRunSummary
            {
                Success = false,
                ErrorMessage = reason
            });
        }

        /// <summary>Trims a trailing ".0" so 1.0% logs/names as "1", while 1.5% stays "1.5".</summary>
        private static string FormatPercent(double percent) =>
            percent % 1 == 0 ? percent.ToString("0") : percent.ToString("0.##");
    }
}
