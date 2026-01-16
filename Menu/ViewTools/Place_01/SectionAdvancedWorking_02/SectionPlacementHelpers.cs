using System.Linq;
using Autodesk.Revit.DB;

namespace Revit22_Plugin.SectionManagerMVVMv4
{
    public static class SectionPlacementHelpers
    {
        /// <summary>
        /// Finds the top-left corner of the title block on the given sheet.
        /// </summary>
        public static XYZ GetTopLeftCorner(ViewSheet sheet)
        {
            var doc = sheet.Document;
            var tbInst = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .FirstOrDefault(fi =>
                    fi.Symbol.Category.Id.IntegerValue ==
                    (int)BuiltInCategory.OST_TitleBlocks);

            if (tbInst != null)
            {
                var bb = tbInst.get_BoundingBox(sheet);
                return new XYZ(bb.Min.X, bb.Max.Y, 0);
            }

            // fallback origin
            return XYZ.Zero;
        }
    }
}
