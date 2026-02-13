// File: GridPlacementService.cs
// REFACTORED - Works within transaction
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.ExternalEvents;
using Revit26_Plugin.APUS_V314.Helpers;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V314.Services
{
    /// <summary>
    /// Grid placement with fixed column width and adaptive row height
    /// CRITICAL: Assumes active transaction exists.
    /// </summary>
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
            double currentY = context.PlacementArea.Origin.Y;
            int currentRow = 0;

            while (currentIndex < sections.Count && currentRow < layout.Rows)
            {
                if (context.ViewModel?.Progress.IsCancelled == true)
                    break;

                // Get views for current row
                var rowItems = sections
                    .Skip(currentIndex)
                    .Take(layout.Columns)
                    .ToList();

                if (!rowItems.Any())
                    break;

                // Calculate row height (tallest view in row)
                double rowHeight = rowItems
                    .Select(x => ViewSizeService.Calculate(x.View).HeightFt)
                    .Max() + layout.VerticalGap;

                // Check vertical fit
                if (currentY - rowHeight < context.PlacementArea.Bottom)
                    break;

                // Place each view in the row
                for (int col = 0; col < rowItems.Count; col++)
                {
                    var item = rowItems[col];

                    if (!CanPlaceView(item.View, sheet.Id))
                    {
                        context.ViewModel?.LogWarning($"SKIPPED (already placed): {item.ViewName}");
                        result.FailedCount++;
                        currentIndex++;
                        continue;
                    }

                    var footprint = ViewSizeService.Calculate(item.View);

                    // Calculate position
                    double x = context.PlacementArea.Origin.X + col * (layout.CellWidth + layout.HorizontalGap);
                    double centerX = x + layout.CellWidth / 2;

                    // Bottom-align view in cell
                    double viewBottomY = currentY - rowHeight + layout.VerticalGap / 2;
                    double centerY = viewBottomY + footprint.HeightFt / 2;

                    XYZ center = new XYZ(centerX, centerY, 0);

                    // Create viewport
                    var vp = Viewport.Create(_doc, sheet.Id, item.View.Id, center);

                    // Set detail number
                    int detailNumber = context.GetNextDetailNumber();
                    var detailParam = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (detailParam != null && !detailParam.IsReadOnly)
                        detailParam.Set(detailNumber.ToString());

                    context.ViewModel?.LogInfo(
                        $"GRID PLACED: {item.ViewName} on {sheet.SheetNumber} (Detail {detailNumber})");
                    context.ViewModel?.Progress.Step();

                    placed++;
                    currentIndex++;
                }

                currentY -= rowHeight;
                currentRow++;
            }

            return placed;
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