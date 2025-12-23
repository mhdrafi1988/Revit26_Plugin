using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V08.Commands.Models;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    /// <summary>
    /// Collects Detail Item family symbols.
    /// Line-based capability is validated at placement time.
    /// </summary>
    public class DetailItemFamilyCollectorService
    {
        public IList<DetailItemSymbolInfo> Collect(Document doc)
        {
            List<DetailItemSymbolInfo> result = new();

            FilteredElementCollector collector =
                new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .OfClass(typeof(FamilySymbol));

            foreach (FamilySymbol symbol in collector)
            {
                // Skip invalid symbols
                if (symbol == null || symbol.Family == null)
                    continue;

                result.Add(new DetailItemSymbolInfo(symbol));
            }

            return result;
        }
    }
}
