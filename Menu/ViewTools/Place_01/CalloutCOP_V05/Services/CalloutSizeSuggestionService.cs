using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP_V06.Services
{
    public static class CalloutSizeSuggestionService
    {
        private const double BaseFactorMm = 5.0;

        public static double GetSuggestedSizeMm(IEnumerable<View> views)
        {
            var scales = views
                .Where(v => v?.Scale > 0)
                .Select(v => v.Scale)
                .ToList();

            if (!scales.Any())
                return 500;

            return scales.Average() * BaseFactorMm;
        }
    }
}
