using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V100.Models;

namespace Revit26_Plugin.Creaser_V100.Services
{
    public class DetailFamilyService
    {
        private readonly Document _doc;
        private readonly ILogService _log;

        public DetailFamilyService(Document document, ILogService log)
        {
            _doc = document;
            _log = log;
        }

        public IList<DetailFamilyOption> GetLineBasedDetailFamilies()
        {
            using (_log.Scope(nameof(DetailFamilyService), "GetLineBasedDetailFamilies"))
            {
                var results = new List<DetailFamilyOption>();

                var symbols = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(s =>
                        s.Family != null &&
                        s.Family.FamilyPlacementType ==
                        FamilyPlacementType.CurveBasedDetail);

                foreach (FamilySymbol symbol in symbols)
                {
                    results.Add(
                        new DetailFamilyOption(
                            $"{symbol.Family.Name} : {symbol.Name}",
                            symbol.Id));

                    _log.Info(nameof(DetailFamilyService),
                        $"Curve-based detail family found: {symbol.Family.Name}:{symbol.Name}");
                }

                _log.Info(nameof(DetailFamilyService),
                    $"Curve-based detail family count = {results.Count}");

                return results;
            }
        }
    }
}
