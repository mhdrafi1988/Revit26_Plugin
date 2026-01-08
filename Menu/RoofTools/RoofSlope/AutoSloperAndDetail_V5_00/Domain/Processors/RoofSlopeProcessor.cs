using Autodesk.Revit.UI;
using Revit26_Plugin.V5_00.Application.Contexts;
using Revit26_Plugin.V5_00.Domain.Models;
using Revit26_Plugin.V5_00.Domain.Services;
using Revit26_Plugin.V5_00.Infrastructure.Revit;
using System.Collections.Generic;

namespace Revit26_Plugin.V5_00.Domain.Processors
{
    /// <summary>
    /// High-level AutoSlope pipeline:
    /// 1) Apply slopes.
    /// 2) Build directed segments.
    /// 3) Clean segments.
    /// 4) Draw detail lines in plan view.
    /// 5) Write result parameters.
    /// </summary>
    public class RoofSlopeProcessor
    {
        private readonly RoofSlopeProcessorService _slopeService = new();
        private readonly PathSegmentBuilderService _segmentBuilder = new();
        private readonly SegmentCleanupService _segmentCleaner = new();
        private readonly DetailLineCreatorService _detailCreator = new();
        private readonly AutoSlopeParameterWriter _paramWriter = new();

        public SlopeResult Process(AutoSlopeContext context, UIDocument uiDoc)
        {
            var roofData = context.RoofData;
            var selectedDrains = new List<DrainItem>(context.SelectedDrains);
            double slopePercent = context.SlopePercent;

            // 1) Apply slopes
            context.Logger.Info("Applying roof slopes...");
            var tupleResult = _slopeService.ApplySlopes(
                roofData,
                selectedDrains,
                slopePercent,
                msg => context.Logger.Info(msg));

            var slopeResult = new SlopeResult
            {
                VerticesModified = tupleResult.modified,
                MaxElevationMm = tupleResult.maxOffsetMm,
                LongestPathMeters = tupleResult.longestPathM
            };

            // 2) Build directed segments
            context.Logger.Info("Building directed path segments...");
            var rawSegments = _segmentBuilder.BuildDirectedSegments(
                _slopeService.LastPathResults);

            // 3) Clean segments
            context.Logger.Info("Cleaning path segments...");
            var cleanSegments = _segmentCleaner.CleanSegments(rawSegments);

            // 4) Draw detail lines (if plan view)
            if (uiDoc != null)
            {
                context.Logger.Info("Creating detail lines in active plan view (if applicable)...");
                _detailCreator.DrawDetailLinesIfPlan(uiDoc, cleanSegments);
            }

            // 5) Write result parameters
            context.Logger.Info("Writing AutoSlope result parameters...");
            _paramWriter.WriteResults(roofData.Roof, slopeResult);

            return slopeResult;
        }
    }
}
