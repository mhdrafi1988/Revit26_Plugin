// ==================================
// File: HorizontalCreaseFilterService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V004_00
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V004_00.Services
{
    /// <summary>
    /// Filters out crease curves where both endpoints have the same Z elevation.
    /// Horizontal creases (flat in the Z direction) are not true roof creases.
    /// </summary>
    public class HorizontalCreaseFilterService
    {
        private readonly LoggingService _log;
        private const double Tol = 1e-6;

        public HorizontalCreaseFilterService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Removes crease curves where both endpoints have identical Z values.
        /// </summary>
        /// <param name="creaseCurves">Original 3D crease curves.</param>
        /// <returns>Filtered list excluding horizontal curves.</returns>
        public IList<Curve> FilterOutHorizontalCreases(IList<Curve> creaseCurves)
        {
            if (creaseCurves == null || creaseCurves.Count == 0)
            {
                _log.Info("No crease curves to filter for horizontal lines.");
                return new List<Curve>();
            }

            var result = new List<Curve>();
            int skipped = 0;

            foreach (Curve curve in creaseCurves)
            {
                if (curve == null)
                    continue;

                XYZ p0 = curve.GetEndPoint(0);
                XYZ p1 = curve.GetEndPoint(1);

                // Check if both endpoints have same Z (within tolerance)
                if (Math.Abs(p0.Z - p1.Z) < Tol)
                {
                    skipped++;
                    _log.Warning($"Horizontal crease removed: Z={p0.Z:F3} (both endpoints same elevation)");
                    continue;
                }

                result.Add(curve);
            }

            _log.Info($"Horizontal crease filter: kept {result.Count}, removed {skipped}");
            return result;
        }
    }
}
