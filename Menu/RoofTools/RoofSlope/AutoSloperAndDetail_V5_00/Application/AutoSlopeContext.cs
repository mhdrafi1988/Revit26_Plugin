using Revit26_Plugin.V5_00.Domain.Models;
using Revit26_Plugin.V5_00.Infrastructure.Logging;
using System.Collections.Generic;

namespace Revit26_Plugin.V5_00.Application.Contexts
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
