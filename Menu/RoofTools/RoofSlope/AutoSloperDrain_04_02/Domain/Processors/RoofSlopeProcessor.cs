// =====================================================
// File: RoofSlopeProcessor.cs
// Purpose: Domain-level processor that coordinates
//          slope application and returns results
// =====================================================

using Revit22_Plugin.V4_02.Application.Contexts;
using Revit22_Plugin.V4_02.Domain.Models;
using Revit22_Plugin.V4_02.Domain.Services;
using System.Linq;

namespace Revit22_Plugin.V4_02.Domain.Processors
{
    /// <summary>
    /// Orchestrates roof slope processing.
    /// This class does NOT touch Revit transactions directly.
    /// It delegates real work to RoofSlopeProcessorService.
    /// </summary>
    public class RoofSlopeProcessor
    {
        private readonly RoofSlopeProcessorService _service;

        public RoofSlopeProcessor()
        {
            _service = new RoofSlopeProcessorService();
        }

        /// <summary>
        /// Executes slope calculation and application for a roof.
        /// </summary>
        public SlopeResult Process(AutoSlopeContext context)
        {
            // -----------------------------
            // Basic safety validation
            // -----------------------------
            if (context == null)
                throw new System.ArgumentNullException(nameof(context));

            if (context.RoofData == null)
                throw new System.InvalidOperationException("RoofData is null.");

            if (context.SelectedDrains == null || !context.SelectedDrains.Any())
                throw new System.InvalidOperationException("No drains selected.");

            context.Logger.Info("Starting roof slope processing...");

            // -----------------------------
            // Apply slopes (REAL WORK)
            // -----------------------------
            var (modified, maxOffsetMm, longestPathM) =
                _service.ApplySlopes(
                    context.RoofData,
                    context.SelectedDrains.ToList(),
                    context.SlopePercent,
                    context.Logger.Info);

            context.Logger.Info("Roof slope processing finished.");

            // -----------------------------
            // Map domain results
            // -----------------------------
            return new SlopeResult
            {
                VerticesModified = modified,
                VerticesSkipped = 0,
                MaxElevationMm = maxOffsetMm,
                LongestPathMeters = longestPathM
            };
        }
    }
}
