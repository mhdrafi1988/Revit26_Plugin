using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V01.Services
{
    public static class DetailItemCollectorService
    {
        public static List<FamilySymbol> GetLineBasedDetailItemSymbols(
            Document doc,
            View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .Where(fi =>
                    fi.Symbol != null &&
                    fi.Symbol.Family != null &&
                    (
                        fi.Symbol.Family.FamilyPlacementType ==
                            FamilyPlacementType.CurveBasedDetail ||
                        fi.Symbol.Family.FamilyPlacementType ==
                            FamilyPlacementType.CurveBased
                    ) &&
                    fi.Location is LocationCurve lc &&
                    lc.Curve is Line
                )
                .GroupBy(fi => fi.Symbol.Id)
                .Select(g => g.First().Symbol)
                .OrderBy(s => s.Family.Name)
                .ThenBy(s => s.Name)
                .ToList();
        }
    }
}
