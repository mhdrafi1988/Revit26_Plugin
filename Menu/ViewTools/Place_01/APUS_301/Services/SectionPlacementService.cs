using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_301.ViewModels;
using System;
using System.Linq;

namespace Revit26_Plugin.APUS_301.Services
{
    public class SectionPlacementService
    {
        private readonly Document _doc;
        private readonly UIApplication _uiapp;

        public SectionPlacementService(Document doc, UIApplication uiapp)
        {
            _doc = doc;
            _uiapp = uiapp;
        }

        public ViewSheet ExecutePlacement(AutoPlaceSectionsViewModel vm)
        {
            var sections = vm.GetSelectedSections().ToList();
            if (!sections.Any())
                return null;

            var sheetService = new SheetService(_doc);

            int sheetIndex = 1;
            ViewSheet sheet = sheetService.Create(vm.SelectedTitleBlock, sheetIndex);

            var size = sheetService.GetSize(sheet);
            double sheetW = size.w;
            double sheetH = size.h;

            // ----------------------------
            // Convert UI values (mm ? ft)
            // ----------------------------
            double margin = MmToFt(vm.MarginMm);
            double reservedRight = MmToFt(vm.ReservedRightMm);
            double hSpacing = MmToFt(vm.HSpacingMm);
            double vSpacing = MmToFt(vm.VSpacingMm);
            double titleGap = MmToFt(vm.TitleGapMm);
            double titleBand = MmToFt(vm.TitleBandMm);

            // ----------------------------
            // Usable sheet area
            // ----------------------------
            double left = margin;
            double right = margin + reservedRight;
            double top = margin;
            double bottom = margin;

            double usableW = sheetW - left - right;
            double usableH = sheetH - top - bottom;

            // ----------------------------
            // Calculate required size per section
            // ----------------------------
            ViewSection refSec = sections.First();
            BoundingBoxXYZ bb = refSec.CropBox;

            double modelW = bb.Max.X - bb.Min.X;
            double modelH = bb.Max.Y - bb.Min.Y;

            int scale = refSec.Scale > 0 ? refSec.Scale : 1;

            double paperW = modelW / scale;
            double paperH = modelH / scale;

            double requiredW = paperW + (2 * hSpacing);
            double requiredH = paperH + (2 * vSpacing) + titleBand + titleGap;

            int cols = Math.Max(1, (int)(usableW / requiredW));
            int rows = Math.Max(1, (int)(usableH / requiredH));

            int col = 0;
            int row = 0;

            foreach (var sec in sections)
            {
                if (row >= rows)
                {
                    sheetIndex++;
                    sheet = sheetService.Create(vm.SelectedTitleBlock, sheetIndex);

                    size = sheetService.GetSize(sheet);
                    sheetW = size.w;
                    sheetH = size.h;

                    col = 0;
                    row = 0;
                }

                double x = left + (col * requiredW) + (requiredW / 2);
                double y = top + (row * requiredH) + (paperH / 2) + titleBand;

                XYZ position = new XYZ(x, y, 0);

                Viewport vp = Viewport.Create(_doc, sheet.Id, sec.Id, position);

                // Apply title offset
                try
                {
                    vp.LabelOffset = new XYZ(0, -titleGap, 0);
                }
                catch { }

                col++;
                if (col >= cols)
                {
                    col = 0;
                    row++;
                }
            }

            return sheet;
        }

        private double MmToFt(double mm)
            => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
