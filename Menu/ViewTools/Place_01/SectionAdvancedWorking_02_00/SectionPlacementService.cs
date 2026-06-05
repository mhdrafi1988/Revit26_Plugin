using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.SectionPlacer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.SectionPlacer.Services
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
            {
                TaskDialog.Show("Info", "No sections selected.");
                return null;
            }

            var existingNames = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Select(v => v.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // ISO 5457 minimum margins (mm): Left=20, Others=10
            double isoLeftFt = UnitUtils.ConvertToInternalUnits(20, UnitTypeId.Millimeters);
            double isoOtherFt = UnitUtils.ConvertToInternalUnits(10, UnitTypeId.Millimeters);

            // From UI (feet)
            double leftMarginFt = Math.Max(vm.Margin, isoLeftFt);
            double rightMarginFt = Math.Max(vm.Margin, isoOtherFt) + vm.ReservedRight;
            double topMarginFt = Math.Max(vm.Margin, isoOtherFt);
            double bottomMarginFt = Math.Max(vm.Margin, isoOtherFt);

            double gapH = vm.HSpacing;
            double gapV = vm.VSpacing;

            double titleReserve = vm.TitleGap + vm.TitleBand;

            var sheetService = new SheetService(_doc, _uiapp);
            int sheetCounter = 1;
            int detailNumber = 1;

            ViewSheet sheet = sheetService.CreateSheet(vm.SelectedTitleBlock, sheetCounter);
            var (sheetW, sheetH) = sheetService.GetSheetSize(sheet);

            double usableW = sheetW - leftMarginFt - rightMarginFt;
            double usableH = sheetH - topMarginFt - bottomMarginFt;

            var packer = new BinPacker(usableW, usableH, topMarginFt, rightMarginFt, bottomMarginFt);

            DrawReservedZones(sheet, sheetW, sheetH, leftMarginFt, rightMarginFt, topMarginFt, bottomMarginFt);

            foreach (var sec in sections)
            {
                double modelW = sec.CropBox.Max.X - sec.CropBox.Min.X;
                double modelH = sec.CropBox.Max.Y - sec.CropBox.Min.Y;
                int scale = sec.Scale > 0 ? sec.Scale : 1;

                double paperW = modelW / scale;
                double paperH = modelH / scale;

                double needW = paperW + 2 * gapH;
                double needH = paperH + titleReserve + 2 * gapV;

                if (needW > usableW || needH > usableH)
                {
                    TaskDialog.Show("Warning",
                        $"{sec.Name} is too large for the current sheet (spacing & title band included). Skipping.");
                    continue;
                }

                var rect = packer.Insert(needW, needH);
                if (rect == null)
                {
                    // new sheet
                    sheetCounter++;
                    sheet = sheetService.CreateSheet(vm.SelectedTitleBlock, sheetCounter);
                    (sheetW, sheetH) = sheetService.GetSheetSize(sheet);

                    usableW = sheetW - leftMarginFt - rightMarginFt;
                    usableH = sheetH - topMarginFt - bottomMarginFt;
                    packer = new BinPacker(usableW, usableH, topMarginFt, rightMarginFt, bottomMarginFt);

                    DrawReservedZones(sheet, sheetW, sheetH, leftMarginFt, rightMarginFt, topMarginFt, bottomMarginFt);

                    rect = packer.Insert(needW, needH);
                    if (rect == null)
                    {
                        TaskDialog.Show("Error", $"{sec.Name} could not be placed even on a new sheet.");
                        continue;
                    }
                }

                // Duplicate names
                // --- Clean Duplicate Name Handler (NN format) ---
                string baseName = sec.Name;
                string newName = baseName;
                int n = 1;

                // If the name already exists, generate Section-01, Section-02, etc.
                while (existingNames.Contains(newName))
                {
                    newName = $"{baseName}-{n++:00}";
                }

                // apply new name if changed
                if (!newName.Equals(baseName))
                {
                    try { sec.Name = newName; } catch { }
                }

                // add final name into hashset to avoid further conflicts
                existingNames.Add(newName);


                // Annotation crop ON + 5 mm offsets
                try
                {
                    var annoCropActive = sec.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
                    if (annoCropActive != null && !annoCropActive.IsReadOnly) annoCropActive.Set(1);

                    var mgr = sec.GetCropRegionShapeManager();
                    if (mgr != null && mgr.CanHaveAnnotationCrop)
                    {
                        double off = UnitUtils.ConvertToInternalUnits(5, UnitTypeId.Millimeters);
                        mgr.LeftAnnotationCropOffset = off;
                        mgr.RightAnnotationCropOffset = off;
                        mgr.TopAnnotationCropOffset = off;
                        mgr.BottomAnnotationCropOffset = off;
                    }
                }
                catch { }

                double x = leftMarginFt + rect.X + gapH + (paperW / 2);
                double y = topMarginFt + rect.Y + gapV + titleReserve + (paperH / 2);
                XYZ pos = new XYZ(x, y, 0);

                try
                {
                    Viewport vp = Viewport.Create(_doc, sheet.Id, sec.Id, pos);

                    var dn = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
                    if (dn != null && !dn.IsReadOnly) dn.Set(detailNumber.ToString());
                    detailNumber++;

                    try
                    {
                        vp.LabelOffset = new XYZ(0, -vm.TitleGap, 0);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Error", $"Failed to place '{sec.Name}' on sheet {sheet.Name}:\n{ex.Message}");
                }
            }

            TaskDialog.Show("Summary",
                $"Placed {sections.Count} section(s) on {sheetCounter} sheet(s)\n" +
                $"H/V spacing applied, bottom title band = {vm.TitleGapMm + vm.TitleBandMm} mm.");

            return sheet;
        }

        private void DrawReservedZones(ViewSheet sheet, double sheetW, double sheetH, double leftMarginFt, double rightMarginFt, double topMarginFt, double bottomMarginFt)
        {
            var lineType = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Lines)
                .WhereElementIsElementType()
                .FirstOrDefault();

            ElementId lineTypeId = lineType?.Id ?? ElementId.InvalidElementId;

            List<Curve> curves = new List<Curve>();

            // Right reserved area
            double xRight = sheetW - rightMarginFt;
            curves.Add(Line.CreateBound(new XYZ(xRight, 0, 0), new XYZ(xRight, sheetH, 0)));

            // Bottom reserved band
            double yBottom = bottomMarginFt;
            curves.Add(Line.CreateBound(new XYZ(0, yBottom, 0), new XYZ(sheetW, yBottom, 0)));

            foreach (var curve in curves)
            {
                DetailCurve dc = _doc.Create.NewDetailCurve(sheet, curve);
                if (lineTypeId != ElementId.InvalidElementId)
                {
                    dc.LineStyle = _doc.GetElement(lineTypeId) as GraphicsStyle;
                }
            }
        }
    }
}
