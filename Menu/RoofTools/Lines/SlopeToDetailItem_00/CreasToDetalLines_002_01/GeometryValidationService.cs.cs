using System;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Final geometry sanity checks.
    /// </summary>
    public class GeometryValidationService
    {
        public void Validate(
            ClassifiedRoofLoops classified,
            LoggingService log)
        {
            if (classified.OuterLoop == null)
            {
                log.Error("Outer loop missing.");
                throw new InvalidOperationException(
                    "No outer boundary detected.");
            }

            if (classified.OuterLoop.Edges.Count < 3)
            {
                log.Error("Outer loop is degenerate.");
                throw new InvalidOperationException(
                    "Outer boundary invalid.");
            }

            log.Info("Geometry validation passed.");
        }
    }
}
