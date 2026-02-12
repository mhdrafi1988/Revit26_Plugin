// File: AdaptiveGridPlacementService.cs
// NEW - Complete implementation
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V317.ExternalEvents;
using Revit26_Plugin.APUS_V317.Models;
using Revit26_Plugin.APUS_V317.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V317.Services
{
    /// <summary>
    /// Adaptive grid placement that groups views by similar sizes
    /// CRITICAL: Assumes active transaction exists.
    /// </summary>
    public class AdaptiveGridPlacementService
    {
        private readonly Document _doc;
        private readonly SheetCreationService _sheetCreator;
        private readonly GridPlacementService _gridPlacer;

        public AdaptiveGridPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetCreator = new SheetCreationService(doc);
            _gridPlacer = new GridPlacementService(doc);
        }

        public SectionPlacementHandler.PlacementResult PlaceSections(
            SectionPlacementHandler.PlacementContext context,
            List<SectionItemViewModel> sections)
        {
            var result = new SectionPlacementHandler.PlacementResult();

            if (sections == null || !sections.Any())
            {
                context.ViewModel?.LogWarning("No sections to place.");
                result.ErrorMessage = "No sections to place";
                return result;
            }

            try
            {
                context.ViewModel?.LogInfo("🔍 Starting adaptive grid placement...");

                // Calculate optimal grid layout
                if (!GridLayoutCalculationService.TryCalculateAdaptive(
                    sections,
                    context.PlacementArea,
                    context.HorizontalGapMm,
                    context.VerticalGapMm,
                    out var layout,
                    context.ViewModel))
                {
                    result.ErrorMessage = "Failed to calculate adaptive grid layout";
                    return result;
                }

                context.ViewModel?.LogInfo($"📊 Adaptive grid: {layout.Columns} columns, Cell: {layout.CellWidth:F2}×{layout.CellHeight:F2} ft");

                // Group sections by height for optimal row packing
                var heightGroups = GroupByHeight(sections, context);
                int totalPlaced = 0;
                int sheetCount = 0;

                foreach (var group in heightGroups)
                {
                    if (context.ViewModel?.Progress.IsCancelled == true)
                        break;

                    if (!group.Any())
                        continue;

                    // Create new sheet for this group
                    string sheetNumber = context.SheetNumberService.GetNextAvailableSheetNumber("AD");
                    context.SheetNumberService.TryReserveSheetNumber(sheetNumber);

                    var sheet = _sheetCreator.Create(context.TitleBlock, sheetNumber, $"Adaptive-{sheetNumber}");
                    context.ViewModel?.LogInfo($"Created sheet: {sheet.SheetNumber}");

                    // Calculate rows that fit this sheet
                    int maxRows = (int)Math.Floor(
                        (context.PlacementArea.Height + layout.VerticalGap) /
                        (layout.CellHeight + layout.VerticalGap));

                    maxRows = Math.Max(1, maxRows);

                    var sheetLayout = new GridLayoutCalculationService.GridLayout
                    {
                        Columns = layout.Columns,
                        Rows = maxRows,
                        CellWidth = layout.CellWidth,
                        CellHeight = layout.CellHeight,
                        HorizontalGap = layout.HorizontalGap,
                        VerticalGap = layout.VerticalGap
                    };

                    // Place views on sheet
                    int placed = _gridPlacer.PlaceOnSheet(
                        sheet,
                        group,
                        0,
                        context,
                        sheetLayout,
                        result);

                    if (placed > 0)
                    {
                        totalPlaced += placed;
                        result.PlacedCount += placed;
                        result.SheetNumbers.Add(sheet.SheetNumber);
                        sheetCount++;

                        context.ViewModel?.LogInfo(
                            $"Placed {placed} views on sheet {sheet.SheetNumber}");
                    }
                    else
                    {
                        // Remove empty sheet
                        _doc.Delete(sheet.Id);
                        context.ViewModel?.LogWarning($"Removed empty sheet: {sheet.SheetNumber}");
                    }
                }

                context.ViewModel?.LogInfo(
                    $"✅ Adaptive grid complete: {totalPlaced} views on {sheetCount} sheets");

                return result;
            }
            catch (Exception ex)
            {
                context.ViewModel?.LogError($"❌ Adaptive grid placement failed: {ex.Message}");
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private List<List<SectionItemViewModel>> GroupByHeight(
            List<SectionItemViewModel> sections,
            SectionPlacementHandler.PlacementContext context)
        {
            var groups = new List<List<SectionItemViewModel>>();
            var sorted = sections
                .Select(s => new
                {
                    Section = s,
                    Height = ViewSizeService.Calculate(s.View).HeightFt
                })
                .OrderByDescending(x => x.Height)
                .ToList();

            var currentGroup = new List<SectionItemViewModel>();
            double? currentHeight = null;
            double heightTolerance = 0.15; // 15% tolerance

            foreach (var item in sorted)
            {
                if (!currentHeight.HasValue)
                {
                    currentGroup.Add(item.Section);
                    currentHeight = item.Height;
                }
                else if (Math.Abs(item.Height - currentHeight.Value) / currentHeight.Value <= heightTolerance)
                {
                    currentGroup.Add(item.Section);
                }
                else
                {
                    if (currentGroup.Any())
                        groups.Add(currentGroup);

                    currentGroup = new List<SectionItemViewModel> { item.Section };
                    currentHeight = item.Height;
                }
            }

            if (currentGroup.Any())
                groups.Add(currentGroup);

            return groups;
        }
    }
}