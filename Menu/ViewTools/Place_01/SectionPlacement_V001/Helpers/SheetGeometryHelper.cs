using Autodesk.Revit.DB;
using System.Linq;

namespace Revit26_Plugin.SectionPlacement_V07.Helpers
{
    public static class SheetGeometryHelper
    {
        public static XYZ GetTopLeftAnchor(ViewSheet sheet)
        {
            var tb = new FilteredElementCollector(sheet.Document, sheet.Id)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault(f =>
                    f.Category.Id.IntegerValue ==
                    (int)BuiltInCategory.OST_TitleBlocks);

            if (tb == null)
                return XYZ.Zero;

            BoundingBoxXYZ bb = tb.get_BoundingBox(sheet);
            return new XYZ(bb.Min.X, bb.Max.Y, 0);
        }
    }
}
