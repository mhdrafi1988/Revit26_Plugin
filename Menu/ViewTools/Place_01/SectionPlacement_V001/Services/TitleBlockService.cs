using Autodesk.Revit.DB;
using Revit26_Plugin.SectionPlacement_V07.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionPlacement_V07.Services
{
    public class TitleBlockService
    {
        private readonly Document _doc;

        public TitleBlockService(Document doc)
        {
            _doc = doc;
        }

        public IList<TitleBlockItem> GetTitleBlocks()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(s => s.Category.Id.Value ==
                            (int)BuiltInCategory.OST_TitleBlocks)
                .OrderBy(s => s.Name)
                .Select(s => new TitleBlockItem(s))
                .ToList();
        }
    }
}
