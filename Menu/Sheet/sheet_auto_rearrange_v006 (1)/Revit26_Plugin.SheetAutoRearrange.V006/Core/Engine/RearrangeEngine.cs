using Autodesk.Revit.DB;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V006.Core.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V006.Core.Engine
{
    /// <summary>
    /// Orchestrates a full Run: deletes unticked viewports, packs ticked
    /// viewports using the selected algorithm against the resolved
    /// PlaceableRegion, and moves each viewport to its new center. One
    /// Transaction per Run (per suite convention — no transaction-in-loop).
    /// Rolls back entirely on failure.
    ///
    /// V006 CHANGE: takes a PlaceableRegion instead of flat usableAreaMin/Max
    /// XYZ. If region is null (caller detected 2+ title blocks on the sheet),
    /// Run does nothing and returns a failed result with an explanatory
    /// message — per confirmed design ("if two title block present just do
    /// nothing").
    /// </summary>
    public class RearrangeEngine
    {
        private readonly ReadingOrderPackingService _readingOrderService = new();
        private readonly SheetOrderPackingService _sheetOrderService = new();

        public RunResult Run(
            Document doc,
            ViewSheet sheet,
            List<ViewOnSheetItem> allItems,
            RearrangeAlgorithm algorithm,
            OverflowHandlingMode overflowMode,
            GapSettings gapSettings,
            double rowToleranceMm,
            RowAlignment rowAlignment,
            BlockAlignmentH blockH,
            BlockAlignmentV blockV,
            PlaceableRegion? region,
            ObservableCollection<LogEntry> log)
        {
            var result = new RunResult { TotalViews = allItems.Count };

            // Sheet has 2+ title blocks — confirmed design says do nothing.
            // No transaction is opened at all; nothing on the sheet changes.
            if (region == null)
            {
                result.Success = false;
                result.ErrorMessage = "Sheet has multiple title blocks — Run skipped, no changes made. Resolve to a single title block and Refresh to retry.";
                log.Add(new LogEntry(LogLevel.Warning, result.ErrorMessage));
                return result;
            }

            var ticked = allItems.Where(i => i.IsChecked).ToList();
            var unticked = allItems.Where(i => !i.IsChecked).ToList();
            result.Selected = ticked.Count;

            if (overflowMode != OverflowHandlingMode.PlaceWhatsPlaceable)
            {
                log.Add(new LogEntry(LogLevel.Warning,
                    $"Overflow mode '{overflowMode}' is not yet implemented in V006 — falling back to Place What's Placeable."));
                overflowMode = OverflowHandlingMode.PlaceWhatsPlaceable;
            }

            log.Add(new LogEntry(LogLevel.Info,
                $"Run started — {ticked.Count} selected, {unticked.Count} to remove. Algorithm: {algorithm}. Region: {region.DisplayText}{(region.IsLShape ? " (L-shape)" : "")}."));

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

                // Step 1 — remove unticked viewports from the sheet.
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
                        // ASSUMPTION: if deletion fails, the viewport remains on
                        // the sheet at its OLD position (Revit didn't apply the
                        // delete) but this item stays unticked in the grid until
                        // the user hits Refresh. Flagging rather than silently
                        // re-including it in packing, since Rafi hasn't specified
                        // desired behavior for a failed removal.
                        log.Add(new LogEntry(LogLevel.Warning, $"Could not remove '{item.ViewName}': {ex.Message}"));
                    }
                }

                // Step 2 — pack ticked views with the selected algorithm against the resolved region.
                List<PackedViewPlacement> placements = algorithm switch
                {
                    RearrangeAlgorithm.ReadingOrder =>
                        _readingOrderService.Pack(ticked, region, gapSettings),

                    RearrangeAlgorithm.SheetOrder =>
                        _sheetOrderService.Pack(ticked, region, gapSettings,
                            rowToleranceMm, rowAlignment, blockH, blockV),

                    _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
                };

                log.Add(new LogEntry(LogLevel.Info, $"Packing complete — {placements.Count(p => p.Fits)} fit, {placements.Count(p => !p.Fits)} overflow."));

                // Step 3 — apply: move EVERY placement, whether it fits inside
                // the usable area or not. Views that don't fit are placed at
                // their computed overflow position (continuing the same row/
                // column layout below the sheet's bottom edge) rather than
                // left untouched at their old spot — this makes overflow
                // views visible and easy to find/drag onto a real sheet,
                // while still being flagged Overflow in the grid/metrics.
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
