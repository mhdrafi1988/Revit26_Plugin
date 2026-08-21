using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP.V019.Services
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

            // ASSUMPTION: aligned this no-scale-data fallback with the new
            // 400mm default (was 500) for consistency. Not explicitly
            // requested - flagging in case you want this fallback to stay
            // independent of the VM's default callout size.
            if (!scales.Any())
                return 400;

            return scales.Average() * BaseFactorMm;
        }
    }
}
