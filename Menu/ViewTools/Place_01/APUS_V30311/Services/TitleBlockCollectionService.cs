using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V311.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V311.Services
{
    /// <summary>
    /// Collects available Title Block types safely.
    /// </summary>
    public class TitleBlockCollectionService
    {
        private readonly Document _doc;

        public TitleBlockCollectionService(Document doc)
        {
            _doc = doc;
        }

        public IList<TitleBlockItemViewModel> Collect()
        {
            return new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .OrderBy(x => x.FamilyName)
                .ThenBy(x => x.Name)
                .Select(x => new TitleBlockItemViewModel(x))
                .ToList();
        }
    }
}
