using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.APUS_301.Services
{
    public class SheetService
    {
        private readonly Document _doc;
        public SheetService(Document doc) => _doc = doc;

        public ViewSheet Create(FamilySymbol tb, int index)
        {
            if (!tb.IsActive) tb.Activate();
            var sheet = ViewSheet.Create(_doc, tb.Id);
            sheet.Name = $"APUS-{index:000}";
            return sheet;
        }

        public (double w, double h) GetSize(ViewSheet sheet)
        {
            var o = sheet.Outline;
            return (o.Max.U - o.Min.U, o.Max.V - o.Min.V);
        }
    }
}
