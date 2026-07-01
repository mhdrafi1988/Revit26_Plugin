// ==================================
// File: MinimumLengthFilterService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V004_00
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V004_00.Services
{
    /// <summary>
    /// Filters crease lines by minimum length threshold.
    /// Lines shorter than the specified minimum are removed.
    /// </summary>
    public class MinimumLengthFilterService
    {
        private readonly LoggingService _log;

        public MinimumLengthFilterService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Filters crease lines by minimum length.
        /// </summary>
        /// <param name="lines">List of 2D crease lines.</param>
        /// <param name="minLengthMm">Minimum length threshold in millimeters.</param>
        /// <returns>Filtered list containing only lines ≥ minLength.</returns>
        public IList<Line> FilterByMinimumLength(IList<Line> lines, double minLengthMm)
        {
            if (lines == null || lines.Count == 0)
            {
                _log.Info("No lines to filter by minimum length.");
                return new List<Line>();
            }

            // Convert mm to Revit internal units (feet)
            // 1 foot = 304.8 mm
            double minLengthFt = minLengthMm / 304.8;

            var result = new List<Line>();
            int skipped = 0;

            foreach (Line line in lines)
            {
                if (line == null)
                    continue;

                double len = line.Length;

                if (len >= minLengthFt)
                {
                    result.Add(line);
                }
                else
                {
                    skipped++;
                    _log.Warning($"Crease skipped (too short): {len * 304.8:F0}mm < {minLengthMm:F0}mm");
                }
            }

            _log.Info($"Minimum length filter: kept {result.Count}, skipped {skipped}");
            return result;
        }
    }
}
