// DetailItemSymbolCollectorService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V00.Services.Logging;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00.Services.DetailItems
{
    /// <summary>
    /// Collects line-based Detail Item (Detail Component) family symbols available in the document.
    /// This avoids naming collision with other modules that also have "DetailItemCollectorService".
    /// </summary>
    public sealed class DetailItemSymbolCollectorService
    {
        private readonly Document _doc;

        public DetailItemSymbolCollectorService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>
        /// Returns active/inactive FamilySymbols of category OST_DetailComponents.
        /// Safe collector: no model modifications.
        /// </summary>
        public IList<FamilySymbol> CollectDetailSymbols(LoggingService log)
        {
            var results = new List<FamilySymbol>();

            try
            {
                // FamilySymbol for detail items is typically in OST_DetailComponents.
                var collector = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_DetailComponents);

                foreach (FamilySymbol sym in collector)
                {
                    if (sym == null) continue;

                    // Optional filter: only line-based symbols (many detail items are line-based).
                    // Not all families expose this cleanly, so we keep it permissive.
                    results.Add(sym);
                }

                log?.Info($"Detail symbols collected: {results.Count}");
            }
            catch (Exception ex)
            {
                log?.Warning($"Failed to collect detail symbols: {ex.Message}");
            }

            return results;
        }
    }
}
