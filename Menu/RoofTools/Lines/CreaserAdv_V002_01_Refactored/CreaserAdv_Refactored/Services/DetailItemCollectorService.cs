// ==================================
// File: DetailItemCollectorService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V003_01
// ==================================

using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V003_01.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V003_01.Services
{
    /// <summary>
    /// Collects all line-based detail component <see cref="FamilySymbol"/>s
    /// available in the document, sorted by family then type name.
    /// </summary>
    public class DetailItemCollectorService
    {
        private readonly Document      _doc;
        private readonly LoggingService _log;

        public DetailItemCollectorService(Document doc, LoggingService log)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<FamilySymbol> Collect()
        {
            _log.Info("Collecting detail item symbols.");

            var symbols =
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .Cast<FamilySymbol>()
                    .Where(IsLineBased)
                    .OrderBy(s => s.FamilyName)
                    .ThenBy(s => s.Name)
                    .ToList();

            _log.Info($"Detail item symbols collected: {symbols.Count}");
            return symbols;
        }

        // A symbol is line-based when it exposes FAMILY_LINE_LENGTH_PARAM.
        private static bool IsLineBased(FamilySymbol symbol)
        {
            if (symbol?.Family == null || !symbol.Family.IsParametric)
                return false;

            return symbol.get_Parameter(BuiltInParameter.FAMILY_LINE_LENGTH_PARAM) != null;
        }
    }
}
