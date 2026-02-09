using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V313.Models;
using Revit26_Plugin.APUS_V313.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V313.Services
{
    /// <summary>
    /// Places sections on a single sheet using Shelf/Row-based layout.
    /// Trusts the input order and NEVER re-sorts.
    /// </summary>
    public class ShelfPlacementService
    {
        private readonly Document _document;

        public ShelfPlacementService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Places sections on a sheet using shelf/row layout.
        /// Returns the number of sections placed on this sheet.
        /// </summary>
        public ShelfPlacementResult PlaceOnSheet(
            ViewSheet sheet,
            IList<SectionItemViewModel> sections,
            int startIndex,
            SheetPlacementArea placementArea,
            double horizontalGapMm,
            double verticalGapMm)
        {
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            if (sections == null) throw new ArgumentNullException(nameof(sections));
            if (placementArea == null) throw new ArgumentNullException(nameof(placementArea));

            // Convert gaps to internal units
            double hGap = UnitUtils.ConvertToInternalUnits(horizontalGapMm, UnitTypeId.Millimeters);
            double vGap = UnitUtils.ConvertToInternalUnits(verticalGapMm, UnitTypeId.Millimeters);

            // Initialize placement state
            double currentX = placementArea.Left; // Start from left edge
            double currentY = placementArea.Top;  // Start from top edge
            double currentRowHeight = 0;
            int placedCount = 0;
            int failedCount = 0;
            var placedViewIds = new List<ElementId>();

            // Process sections in the exact order provided
            for (int i = startIndex; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section?.View == null)
                {
                    failedCount++;
                    continue;
                }

                // Get view footprint (paper space size)
                var footprint = ViewSizeService.Calculate(section.View);
                double viewWidth = footprint.WidthFt;
                double viewHeight = footprint.HeightFt;

                // Check horizontal fit - wrap to new row if needed
                if (currentX + viewWidth > placementArea.Right)
                {
                    // Wrap to new row
                    currentX = placementArea.Left;
                    currentY -= currentRowHeight + vGap;
                    currentRowHeight = 0;

                    // Reset row tracking for new row
                }

                // Check vertical fit - stop if no more room
                if (currentY - viewHeight < placementArea.Bottom)
                {
                    // No vertical space for this view
                    break;
                }

                // Calculate viewport center point
                // Views are BOTTOM-aligned in the same row
                double viewCenterX = currentX + (viewWidth / 2.0);
                double viewCenterY = currentY - (viewHeight / 2.0);
                XYZ centerPoint = new XYZ(viewCenterX, viewCenterY, 0);

                try
                {
                    // Create viewport
                    Viewport viewport = Viewport.Create(_document, sheet.Id, section.View.Id, centerPoint);
                    placedViewIds.Add(viewport.Id);
                    placedCount++;

                    // Update row height (tallest view in current row)
                    currentRowHeight = Math.Max(currentRowHeight, viewHeight);

                    // Move X position for next view
                    currentX += viewWidth + hGap;
                }
                catch (Exception ex)
                {
                    // Log placement failure
                    failedCount++;
                    // Continue with next section
                }
            }

            return new ShelfPlacementResult
            {
                PlacedCount = placedCount,
                FailedCount = failedCount,
                PlacedViewIds = placedViewIds,
                NextStartIndex = startIndex + placedCount + failedCount
            };
        }
    }

    /// <summary>
    /// Result of placing sections on a single sheet
    /// </summary>
    public class ShelfPlacementResult
    {
        public int PlacedCount { get; set; }
        public int FailedCount { get; set; }
        public List<ElementId> PlacedViewIds { get; set; } = new List<ElementId>();
        public int NextStartIndex { get; set; }
    }
}