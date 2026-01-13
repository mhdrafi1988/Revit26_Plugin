using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionPlacement_V07.Helpers;
using Revit26_Plugin.SectionPlacement_V07.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionPlacement_V07.Services
{
    public class SheetPlacementService
    {
        private readonly Document _doc;

        public SheetPlacementService(Document doc)
        {
            _doc = doc;
        }

        public void PlaceSections(
            IList<SectionItem> sections,
            TitleBlockItem titleBlock,
            int rows,
            int columns,
            double hGapMm,
            double vGapMm,
            UIDocument uidoc)
        {
            int perSheet = rows * columns;
            double hGap = UnitUtils.ConvertToInternalUnits(hGapMm, UnitTypeId.Millimeters);
            double vGap = UnitUtils.ConvertToInternalUnits(vGapMm, UnitTypeId.Millimeters);

            using Transaction tx = new(_doc, "Place Sections");
            tx.Start();

            ElementId lastSheetId = ElementId.InvalidElementId;
            int index = 0;

            while (index < sections.Count)
            {
                var batch = sections.Skip(index).Take(perSheet).ToList();
                index += perSheet;

                var sheet = ViewSheet.Create(_doc, titleBlock.SymbolId);
                lastSheetId = sheet.Id;

                XYZ origin = SheetGeometryHelper.GetTopLeftAnchor(sheet);

                for (int i = 0; i < batch.Count; i++)
                {
                    int r = i / columns;
                    int c = i % columns;

                    XYZ pt = new(
                        origin.X + c * hGap,
                        origin.Y - r * vGap,
                        0);

                    Viewport.Create(_doc, sheet.Id, batch[i].ViewId, pt);
                }
            }

            tx.Commit();

            if (lastSheetId != ElementId.InvalidElementId)
            {
                uidoc.RequestViewChange(_doc.GetElement(lastSheetId) as View);
            }
        }
    }
}
