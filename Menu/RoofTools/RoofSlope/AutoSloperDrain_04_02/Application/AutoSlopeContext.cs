using Revit22_Plugin.V4_02.Domain.Models;
using Revit22_Plugin.V4_02.Infrastructure.Logging;
using System.Collections.Generic;

namespace Revit22_Plugin.V4_02.Application.Contexts
{
    public class AutoSlopeContext
    {
        public RoofData RoofData { get; init; }

        public IReadOnlyList<DrainItem> SelectedDrains { get; init; }

        public double SlopePercent { get; init; }

        public double ThresholdMeters { get; init; }

        public IAutoSlopeLogger Logger { get; init; }
    }
}
