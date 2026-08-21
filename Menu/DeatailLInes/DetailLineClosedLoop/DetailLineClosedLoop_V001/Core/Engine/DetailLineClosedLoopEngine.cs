using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Revit26_Plugin.DetailLineClosedLoop.V001.Core.Models;
using Revit26_Plugin.DetailLineClosedLoop.V001.Core.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Engine
{
    /// <summary>Orchestrates steps 1–7 in order, logging progress/counts at each stage.</summary>
    public static class DetailLineClosedLoopEngine
    {
        public static ProcessResult Run(Document doc, View view, ICollection<ElementId> selectedIds, bool snapEndpoints, double gapToleranceFeet, ObservableCollection<LogEntry> log)
        {
            var result = new ProcessResult();

            double shortCurveTolerance = doc.Application.ShortCurveTolerance;
            double topologyEpsilon = Math.Min(1e-6, shortCurveTolerance);

            double shortCurveMm = UnitUtils.ConvertFromInternalUnits(shortCurveTolerance, UnitTypeId.Millimeters);
            double gapMm = UnitUtils.ConvertFromInternalUnits(gapToleranceFeet, UnitTypeId.Millimeters);
            log.Add(new LogEntry(LogLevel.Debug, $"Tolerance: short curve = {shortCurveMm:F3} mm, gap = {gapMm:F2} mm, snap endpoints = {snapEndpoints}"));

            List<Curve> curves = CurveCollectionService.CollectCurves(doc, selectedIds, log);
            if (curves.Count == 0)
            {
                result.Success = false;
                result.FailedCount = 1;
                result.ErrorMessage = "No detail line/arc curves found in selection.";
                log.Add(new LogEntry(LogLevel.Error, result.ErrorMessage));
                return result;
            }

            curves = CurveIntersectionService.TrimAndExtend(curves, shortCurveTolerance, gapToleranceFeet, log);
            curves = CurveMergeService.MergeOverlappingCollinear(curves, shortCurveTolerance, out int mergedCount, log);
            curves = EngulfedLineFilterService.RemoveEngulfed(curves, shortCurveTolerance, out int removedCount, log);

            if (snapEndpoints)
                curves = EndpointSnapService.Snap(curves, shortCurveTolerance, out _, log);

            curves = GapClosureService.CloseGaps(curves, gapToleranceFeet, shortCurveTolerance, out int gapsClosedCount, out _, log);

            result.MergedCount = mergedCount;
            result.RemovedCount = removedCount;
            result.GapsClosedCount = gapsClosedCount;

            bool built = LoopAssemblyService.TryBuildLoop(curves, Math.Max(shortCurveTolerance, topologyEpsilon), out CurveLoop loop, out string error);
            if (!built)
            {
                result.Success = false;
                result.FailedCount = 1;
                result.ErrorMessage = error;
                log.Add(new LogEntry(LogLevel.Error, $"Loop assembly failed: {error}"));
                return result;
            }

            result.Success = true;
            result.Loop = loop;
            result.CurvesInLoop = curves.Count;
            log.Add(new LogEntry(LogLevel.Success, $"CurveLoop validated: closed, non-self-intersecting, {curves.Count} curves"));
            return result;
        }
    }
}
