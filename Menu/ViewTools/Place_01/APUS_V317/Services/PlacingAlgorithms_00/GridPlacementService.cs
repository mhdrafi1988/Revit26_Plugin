// File: GridPlacementService.cs
// MODIFIED: Height-aware row packing with shelf placement
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V317.ExternalEvents;
using Revit26_Plugin.APUS_V317.Helpers;
using Revit26_Plugin.APUS_V317.Models;
using Revit26_Plugin.APUS_V317.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V317.Services
{
    public class GridPlacementService
    {
        private readonly Document _doc;

        public GridPlacementService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public int PlaceOnSheet(
            ViewSheet sheet,
            IList<SectionItemViewModel> sections,
            int startIndex,
            SectionPlacementHandler.PlacementContext context,
            GridLayoutCalculationService.GridLayout layout,
            SectionPlacementHandler.PlacementResult result)
        {
            if (sections == null || !sections.Any())
                return 0;

            int placed = 0;
            int currentIndex = startIndex;

            double left = context.PlacementArea.Origin.X;
            double right = context.PlacementArea.Right;
            double top = context.PlacementArea.Origin.Y;
            double bottom = context.PlacementArea.Bottom;

            double currentY = top - layout.VerticalGap;
            double gapX = UnitConversionHelper.MmToFeet(context.HorizontalGapMm);
            double gapY = UnitConversionHelper.MmToFeet(context.VerticalGapMm);

            while (currentIndex < sections.Count)
            {
                if (context.ViewModel?.Progress.IsCancelled == true)
                    break;

                // Get remaining sections for this row
                var remainingSections = sections.Skip(currentIndex).ToList();

                // TALLEST FIRST: Find the tallest view in remaining batch
                var tallest = remainingSections
                    .Select(s => new {
                        Section = s,
                        Height = ViewSizeService.Calculate(s.View).HeightFt
                    })
                    .OrderByDescending(x => x.Height)
                    .FirstOrDefault();

                if (tallest == null)
                    break;

                double rowHeight = tallest.Height + gapY;

                // Check if row fits vertically
                if (currentY - rowHeight < bottom + gapY)
                    break;

                // SHELF PACKING: Place tallest first, then fill remaining space
                double rowX = left + gapX;
                double rowBottomY = currentY - rowHeight + gapY;

                // Place the tallest view
                var tallestItem = tallest.Section;

                if (CanPlaceView(tallestItem.View, sheet.Id))
                {
                    double tallestWidth = ViewSizeService.Calculate(tallestItem.View).WidthFt;

                    // Check if fits horizontally
                    if (rowX + tallestWidth + gapX <= right)
                    {
                        // Place tallest at leftmost position
                        double centerX = rowX + tallestWidth / 2;
                        double centerY = rowBottomY + tallest.Height / 2;

                        CreateViewport(sheet, tallestItem, centerX, centerY, context.GetNextDetailNumber());
                        context.ViewModel?.LogInfo($"📏 TALLEST: {tallestItem.ViewName} (H:{tallest.Height:F2}ft)");

                        rowX += tallestWidth + gapX;
                        placed++;
                        currentIndex = FindAndRemove(ref sections, currentIndex, tallestItem);
                        context.ViewModel?.Progress.Step();
                    }
                }

                // Fill remaining space with smaller views
                bool placedAny;
                do
                {
                    placedAny = false;

                    // Find a view that fits in remaining width
                    var fitView = sections.Skip(currentIndex)
                        .Where(s => CanPlaceView(s.View, sheet.Id))
                        .Select(s => new {
                            Section = s,
                            Width = ViewSizeService.Calculate(s.View).WidthFt,
                            Height = ViewSizeService.Calculate(s.View).HeightFt
                        })
                        .Where(x => x.Width <= (right - rowX - gapX) && x.Height <= rowHeight)
                        .OrderByDescending(x => x.Width) // Largest that fits first
                        .FirstOrDefault();

                    if (fitView != null)
                    {
                        double centerX = rowX + fitView.Width / 2;
                        double centerY = rowBottomY + fitView.Height / 2;

                        CreateViewport(sheet, fitView.Section, centerX, centerY, context.GetNextDetailNumber());
                        context.ViewModel?.LogInfo($"   ├─ FILL: {fitView.Section.ViewName} (W:{fitView.Width:F2}ft)");

                        rowX += fitView.Width + gapX;
                        placed++;
                        currentIndex = FindAndRemove(ref sections, currentIndex, fitView.Section);
                        context.ViewModel?.Progress.Step();
                        placedAny = true;
                    }

                } while (placedAny && rowX + gapX < right);

                // Move to next row
                currentY -= rowHeight;

                // Log row utilization
                double rowUtilization = (rowX - left - gapX) / (right - left - gapX * 2);
                context.ViewModel?.LogInfo($"   📊 Row utilization: {rowUtilization:P0}");
            }

            return placed;
        }

        private int FindAndRemove(ref IList<SectionItemViewModel> sections, int startIndex, SectionItemViewModel itemToRemove)
        {
            for (int i = startIndex; i < sections.Count; i++)
            {
                if (sections[i].View.Id == itemToRemove.View.Id)
                {
                    sections.RemoveAt(i);
                    return i; // Return same index since list shifted
                }
            }
            return startIndex;
        }

        private void CreateViewport(ViewSheet sheet, SectionItemViewModel item, double centerX, double centerY, int detailNumber)
        {
            XYZ center = new XYZ(centerX, centerY, 0);
            var vp = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

            var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
            if (detailParam != null && !detailParam.IsReadOnly)
            {
                detailParam.Set(detailNumber.ToString());
            }
        }

        private bool CanPlaceView(ViewSection view, ElementId sheetId)
        {
            try
            {
                return Viewport.CanAddViewToSheet(_doc, sheetId, view.Id);
            }
            catch
            {
                return false;
            }
        }
    }
}