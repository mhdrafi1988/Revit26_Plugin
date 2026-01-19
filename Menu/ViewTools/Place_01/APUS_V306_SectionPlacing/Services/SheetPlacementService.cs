using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V306.Models;
using Revit26_Plugin.APUS_V306.ViewModels;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V306.Services
{
    public class SheetPlacementService
    {
        private readonly Document _doc;

        public SheetPlacementService(Document doc)
        {
            _doc = doc;
        }

        public int PlaceBatch(
            ViewSheet sheet,
            IList<SectionItemViewModel> sortedSections,
            SheetPlacementArea area,
            double gapFt,
            AutoPlaceSectionsViewModel vm,
            ref int detailIndex)
        {
            var packer = new BinPackerService(
                area.Width,
                area.Height);

            int placedCount = 0;

            foreach (var s in sortedSections)
            {
                SectionFootprint fp =
                    ViewSizeService.Calculate(s.View);

                double w = fp.WidthFt + gapFt;
                double h = fp.HeightFt + gapFt;

                if (!packer.TryPlace(w, h, out double px, out double py))
                    break;

                XYZ point = new XYZ(
                    area.Origin.X + px + fp.WidthFt / 2,
                    area.Origin.Y - py - fp.HeightFt / 2,
                    0);

                Viewport viewport = Viewport.Create(
                    _doc,
                    sheet.Id,
                    s.View.Id,
                    point);

                // Continuous detail numbering
                detailIndex++;
                Parameter detailParam =
                    viewport.get_Parameter(
                        BuiltInParameter.VIEWPORT_DETAIL_NUMBER);

                if (detailParam != null && !detailParam.IsReadOnly)
                    detailParam.Set(detailIndex.ToString());

                vm.LogInfo(
                    $"Placed {s.Name} ? Detail {detailIndex}");

                vm.Progress.Step();
                placedCount++;
            }

            return placedCount;
        }
    }
}
