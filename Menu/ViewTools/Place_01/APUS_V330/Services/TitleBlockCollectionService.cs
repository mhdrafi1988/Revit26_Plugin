// File: Services/TitleBlockCollectionService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V330.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V330.Services
{
    public class TitleBlockCollectionService
    {
        private readonly Document _doc;

        public TitleBlockCollectionService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public List<TitleBlockItemViewModel> Collect()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>()
                .Where(fs => fs.IsValidObject)
                .Select(fs => new TitleBlockItemViewModel(fs))
                .OrderBy(tb => tb.DisplayName)
                .ToList();
        }
    }
}
