using Revit26_Plugin.APUS_V312.Models;
using Revit26_Plugin.APUS_V312.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V312.Services
{
    /// <summary>
    /// FEET-ONLY SERVICE
    /// Calculates grid column width and column count using paper-space feet.
    /// UI values (mm) must be converted BEFORE calling this service.
    /// </summary>
    public static class GridLayoutCalculationService
    {
        public static bool TryCalculate(
            IList<SectionItemViewModel> sections,
            SheetPlacementArea area,   // feet
            out double cellWidthFt,    // feet
            out int columns)
        {
            cellWidthFt = 0;
            columns = 0;

            if (sections == null || sections.Count == 0)
                return false;

            List<double> widthsFt =
                sections
                    .Select(x => ViewSizeService.Calculate(x.View).WidthFt)
                    .Where(w => w > 0)
                    .OrderBy(w => w)
                    .ToList();

            if (!widthsFt.Any())
                return false;

            int count = widthsFt.Count;
            double medianWidthFt =
                count % 2 == 1
                    ? widthsFt[count / 2]
                    : (widthsFt[count / 2 - 1] + widthsFt[count / 2]) / 2.0;

            if (medianWidthFt <= 0)
                return false;

            cellWidthFt = medianWidthFt;

            columns = (int)Math.Floor(area.Width / cellWidthFt);

            return columns > 0;
        }
    }
}
