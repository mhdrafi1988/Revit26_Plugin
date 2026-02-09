// File: SheetPlacementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V313.Helpers;
using Revit26_Plugin.APUS_V313.Models;
using Revit26_Plugin.APUS_V313.ViewModels;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V313.Services
{
    public class SheetPlacementService
    {
        private readonly Autodesk.Revit.DB.Document _document;

        public SheetPlacementService(Autodesk.Revit.DB.Document doc)
        {
            _document = doc;
        }

        public int PlaceOnSheet(
            ViewSheet sheet,
            IList<SectionItemViewModel> sections,
            int startIndex,
            SheetPlacementArea area,
            double horizontalGapMm,
            double verticalGapMm,
            ref int placedCount,
            ref int failedCount)
        {
            double hGap = UnitConversionHelper.MmToFeet(horizontalGapMm);
            double vGap = UnitConversionHelper.MmToFeet(verticalGapMm);

            double currentX = area.Origin.X; // Left
            double currentY = area.Origin.Y; // Top
            double rowMaxHeight = 0;

            int placedOnThisSheet = 0;

            for (int i = startIndex; i < sections.Count; i++)
            {
                var sec = sections[i];

                // Get view dimensions from ViewSizeService
                var footprint = ViewSizeService.Calculate(sec.View);
                double w = footprint.WidthFt;
                double h = footprint.HeightFt;

                // Wrap row
                if (currentX + w > area.Origin.X + area.Width) // Right
                {
                    currentX = area.Origin.X; // Left
                    currentY -= rowMaxHeight + vGap;
                    rowMaxHeight = 0;
                }

                // Check vertical overflow ? new sheet
                if (currentY - h < area.Origin.Y - area.Height) // Bottom
                    break;

                // Center point for viewport
                XYZ center = new XYZ(
                    currentX + w / 2.0,
                    currentY - h / 2.0,
                    0);

                try
                {
                    if (Viewport.CanAddViewToSheet(_document, sheet.Id, sec.View.Id))
                    {
                        Viewport.Create(_document, sheet.Id, sec.View.Id, center);
                        placedCount++;
                        placedOnThisSheet++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch
                {
                    failedCount++;
                }

                rowMaxHeight = System.Math.Max(rowMaxHeight, h);
                currentX += w + hGap;
            }

            return placedOnThisSheet;
        }
    }
}