using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V010.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V010.Core.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V010.Core.Engine
{
    /// <summary>
    /// Orchestrates a full Run: deletes unticked viewports, packs ticked
    /// viewports, and moves each viewport to its new center. One Transaction
    /// per Run (per suite convention — no transaction-in-loop). Rolls back
    /// entirely on failure.
    ///
    /// V009 CHANGE: algorithm selection removed from the UI — Sheet Order is
    /// now the only algorithm, called directly (no more switch/dispatch on
    /// a RearrangeAlgorithm parameter). ReadingOrderPackingService still
    /// exists as a file but is no longer invoked here.
    ///
    /// V009 CHANGE: with the Placeable Area UI (including manual override)
    /// removed, a null region at Run time can ONLY mean "no title block
    /// found, or its geometry could not be read" — the caller
    /// (SheetAutoRearrangeEventHandler.ExecuteRun) already checks
    /// MultipleTitleBlocksFound separately BEFORE calling Run, so that case
    /// never reaches here. Message updated accordingly.
    /// </summary>
    public class RearrangeEngine
    {
        private readonly SheetOrderPackingService _sheetOrderService = new();

        public RunResult Run(
            Document doc,
            ViewSheet sheet,
            List<ViewOnSheetItem> allItems,
            OverflowHandlingMode overflowMode,
            GapSettings gapSettings,
            double rowToleranceMm,
            RowAlignment rowAlignment,
            BlockAlignmentH columnAlignH,
            BlockAlignmentV columnAlignV,
            TallWideDetectionSettings tallSettings,
            TallWideDetectionSettings wideSettings,
            PlaceableRegion? region,
            ObservableCollection<LogEntry> log)
        {
            var result = new RunResult { TotalViews = allItems.Count };

            if (region == null)
            {
                result.Success = false;
                result.ErrorMessage = "No title block detected on this sheet (or its geometry could not be read) — Run skipped, no changes made.";
                log.Add(new LogEntry(LogLevel.Warning, result.ErrorMessage));
                return result;
            }

            var ticked = allItems.Where(i => i.IsChecked).ToList();
            var unticked = allItems.Where(i => !i.IsChecked).ToList();
            result.Selected = ticked.Count;

            if (overflowMode != OverflowHandlingMode.PlaceWhatsPlaceable)
            {
                log.Add(new LogEntry(LogLevel.Warning,
                    $"Overflow mode '{overflowMode}' is not yet implemented — falling back to Place What's Placeable."));
                overflowMode = OverflowHandlingMode.PlaceWhatsPlaceable;
            }

            if (tallSettings.IsEnabled || wideSettings.IsEnabled)
            {
                log.Add(new LogEntry(LogLevel.Info,
                    $"Tall/Wide detection active — Tall: {(tallSettings.IsEnabled ? $"{tallSettings.Multiplier}x ±{tallSettings.TolerancePercent}%" : "off")}, " +
                    $"Wide: {(wideSettings.IsEnabled ? $"{wideSettings.Multiplier}x ±{wideSettings.TolerancePercent}%" : "off")}."));
            }

            log.Add(new LogEntry(LogLevel.Info,
                $"Run started — {ticked.Count} selected, {unticked.Count} to remove."));

            using var tx = new Transaction(doc, "Sheet Auto Rearrange");
            try
            {
                var txStatus = tx.Start();
                if (txStatus != TransactionStatus.Started)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Transaction failed to start (status: {txStatus}).";
                    log.Add(new LogEntry(LogLevel.Error, result.ErrorMessage));
                    return result;
                }

                foreach (var item in unticked)
                {
                    try
                    {
                        doc.Delete(item.ViewportId);
                        item.FitStatus = ViewFitStatus.NotSelected;
                        result.Removed++;
                        log.Add(new LogEntry(LogLevel.Info, $"Removed from sheet: {item.ViewName} (viewport {item.ViewportId.Value})."));
                    }
                    catch (Exception ex)
                    {
                        log.Add(new LogEntry(LogLevel.Warning, $"Could not remove '{item.ViewName}': {ex.Message}"));
                    }
                }

                List<PackedViewPlacement> placements = _sheetOrderService.Pack(
                    ticked, region, gapSettings,
                    rowToleranceMm, rowAlignment, columnAlignH, columnAlignV,
                    tallSettings, wideSettings);

                log.Add(new LogEntry(LogLevel.Info, $"Packing complete — {placements.Count(p => p.Fits)} fit, {placements.Count(p => !p.Fits)} overflow."));

                foreach (var placement in placements)
                {
                    try
                    {
                        if (doc.GetElement(placement.Item.ViewportId) is Viewport vp)
                        {
                            XYZ currentCenter = vp.GetBoxCenter();
                            XYZ delta = placement.NewCenter - currentCenter;
                            ElementTransformUtils.MoveElement(doc, vp.Id, delta);

                            if (placement.Fits)
                            {
                                placement.Item.FitStatus = ViewFitStatus.Fits;
                                result.PlacedSuccessfully++;
                                log.Add(new LogEntry(LogLevel.Success, $"Placed: {placement.Item.ViewName}."));
                            }
                            else
                            {
                                placement.Item.FitStatus = ViewFitStatus.Overflow;
                                result.FailedToFit++;
                                log.Add(new LogEntry(LogLevel.Warning, $"'{placement.Item.ViewName}' does not fit — placed below the sheet (Place What's Placeable)."));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        placement.Item.FitStatus = ViewFitStatus.Overflow;
                        result.FailedToFit++;
                        log.Add(new LogEntry(LogLevel.Error, $"Failed to place '{placement.Item.ViewName}': {ex.Message}"));
                    }
                }

                var commitStatus = tx.Commit();
                if (commitStatus != TransactionStatus.Committed)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Transaction failed to commit (status: {commitStatus}).";
                    log.Add(new LogEntry(LogLevel.Error, result.ErrorMessage));
                    return result;
                }

                result.Success = true;
                log.Add(new LogEntry(LogLevel.Success,
                    $"Run complete — {result.PlacedSuccessfully} placed | {result.Removed} removed | {result.FailedToFit} failed."));
                return result;
            }
            catch (Exception ex)
            {
                if (tx.HasStarted() && !tx.HasEnded())
                    tx.RollBack();

                result.Success = false;
                result.ErrorMessage = ex.Message;
                log.Add(new LogEntry(LogLevel.Error, $"Run failed, rolled back: {ex.Message}"));
                return result;
            }
        }
    }
}
