using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Parameters;
using Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Core.Engine
{
    public static class AutoSlopeEngine
    {
        public static void Execute(UIApplication app, AutoSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;
            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null)
                return;

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsValidObject)
            {
                data.Log(LogColorHelper.Red("Roof slab shape editor is not available. Aborting."));
                return;
            }

            // Reset vertices
            using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
            {
                tx.Start();

                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                {
                    editor.ModifySubElement(v, 0);
                }

                tx.Commit();
            }

            List<SlabShapeVertex> vertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
            {
                vertices.Add(v);
            }

            double slopeFactor = data.SlopePercent / 100.0;
            double thresholdFt = UnitUtils.ConvertToInternalUnits(data.ThresholdMeters, UnitTypeId.Meters);

            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log(LogColorHelper.Red("Top face not found. Aborting."));
                return;
            }

            // Build final drain points from user picks + nearby roof shape points.
            List<XYZ> finalDrainPoints = data.DrainPoints ?? new List<XYZ>();

            if (data.EnableDrainTolerance && data.DrainToleranceMm > 0)
            {
                data.Log(LogColorHelper.Cyan(
                    $"🔍 Checking for nearby roof shape points within {data.DrainToleranceMm}mm of selected points..."));

                finalDrainPoints = DrainDetectionHelper.DetectDrainsWithinRadius(
                    roof,
                    finalDrainPoints,
                    data.DrainToleranceMm,
                    data.Log);

                finalDrainPoints = DrainDetectionHelper.RemoveDuplicates(
                    finalDrainPoints,
                    data.DrainToleranceMm);
            }

            if (finalDrainPoints == null || finalDrainPoints.Count == 0)
            {
                data.Log(LogColorHelper.Red("No drain points are available. Aborting."));
                return;
            }

            var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFt);

            double drainMatchToleranceFt = data.EnableDrainTolerance && data.DrainToleranceMm > 0
                ? UnitUtils.ConvertToInternalUnits(data.DrainToleranceMm, UnitTypeId.Millimeters)
                : 0.001;

            HashSet<int> drainIndices = new HashSet<int>();
            for (int i = 0; i < vertices.Count; i++)
            {
                foreach (XYZ drainPoint in finalDrainPoints)
                {
                    if (drainPoint == null)
                        continue;

                    if (vertices[i].Position.DistanceTo(drainPoint) <= drainMatchToleranceFt)
                    {
                        drainIndices.Add(i);
                        break;
                    }
                }
            }

            if (drainIndices.Count == 0)
            {
                data.Log(LogColorHelper.Red("No roof vertices matched the selected drain points. Aborting."));
                return;
            }

            int processed = 0;
            int skipped = 0;
            double maxElevFt = 0;
            double maxPathFt = 0;
            List<VertexData> vertexDataList = new List<VertexData>();
            Stopwatch sw = Stopwatch.StartNew();

            using (Transaction tx = new Transaction(doc, "Apply AutoSlope"))
            {
                tx.Start();

                for (int i = 0; i < vertices.Count; i++)
                {
                    double pathFt = dijkstra.ComputeShortestPath(i, drainIndices);

                    if (double.IsInfinity(pathFt) || pathFt > thresholdFt)
                    {
                        skipped++;

                        if (data.ExportConfig?.IncludeVertexDetails == true)
                        {
                            vertexDataList.Add(new VertexData
                            {
                                VertexIndex = i,
                                Position = vertices[i].Position,
                                PathLengthMeters = double.IsInfinity(pathFt)
                                    ? 0
                                    : UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                                ElevationOffsetMm = 0,
                                NearestDrainIndex = -1,
                                DirectionVector = XYZ.Zero,
                                WasProcessed = false
                            });
                        }

                        continue;
                    }

                    double elevFt = pathFt * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    if (elevFt > maxElevFt)
                        maxElevFt = elevFt;

                    if (pathFt > maxPathFt)
                        maxPathFt = pathFt;

                    int nearestDrainIndex = FindNearestDrainIndex(vertices[i].Position, finalDrainPoints);
                    XYZ directionVector = nearestDrainIndex >= 0
                        ? CalculateDirectionVector(vertices[i].Position, finalDrainPoints[nearestDrainIndex])
                        : XYZ.Zero;

                    vertexDataList.Add(new VertexData
                    {
                        VertexIndex = i,
                        Position = vertices[i].Position,
                        PathLengthMeters = UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                        ElevationOffsetMm = UnitUtils.ConvertFromInternalUnits(elevFt, UnitTypeId.Millimeters),
                        NearestDrainIndex = nearestDrainIndex,
                        DirectionVector = directionVector,
                        WasProcessed = true
                    });
                }

                tx.Commit();
            }

            sw.Stop();

            int highest_mm = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);

            double longest_m = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2,
                MidpointRounding.AwayFromZero);

            int durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);
            string runDate = DateTime.Now.ToString("dd-MM-yy HH:mm");

            var metrics = new AutoSlopeMetrics
            {
                Processed = processed,
                Skipped = skipped,
                HighestElevation = highest_mm,
                LongestPath = longest_m
            };

            data.Vm.VerticesProcessed = processed;
            data.Vm.VerticesSkipped = skipped;
            data.Vm.HighestElevation_mm = highest_mm;
            data.Vm.LongestPath_m = longest_m;
            data.Vm.DrainCount = finalDrainPoints.Count;
            data.Vm.RunDuration_sec = durationSec;
            data.Vm.RunDate = runDate;

            AutoSlopeParameterWriter.WriteAll(
                doc,
                roof,
                data,
                highest_mm,
                maxPathFt,
                processed,
                skipped,
                durationSec,
                finalDrainPoints.Count,
                "P.04.00");

            // Export to Excel if enabled
            if (data.ExportConfig?.ExportToExcel == true)
            {
                string compactPath = ExcelExportHelper.ExportCompactVertexData(
                    data,
                    vertexDataList,
                    roof,
                    data.SlopePercent);

                if (!string.IsNullOrEmpty(compactPath))
                {
                    data.Log(LogColorHelper.Green($"✅ Compact Excel data exported to: {compactPath}"));
                    data.Log(LogColorHelper.Cyan("  • Sorted by PathLength_Meters (longest first)"));
                    data.Log(LogColorHelper.Cyan($"  • Contains {processed} processed vertices"));

                    var longestVertex = vertexDataList
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .FirstOrDefault();

                    if (longestVertex != null)
                    {
                        data.Log(LogColorHelper.Cyan(
                            $"  • Longest path: {longestVertex.PathLengthMeters:F2} m to drain {longestVertex.NearestDrainIndex}"));
                    }
                }

                if (data.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = ExcelExportHelper.ExportDetailedVertexData(
                        data,
                        vertexDataList,
                        roof,
                        finalDrainPoints,
                        data.SlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        data.Log(LogColorHelper.Green($"✅ Detailed Excel data exported to: {detailedPath}"));
                        data.Log(LogColorHelper.Cyan(
                            $"  • Contains {vertexDataList.Count} total vertices ({processed} processed, {skipped} skipped)"));
                        data.Log(LogColorHelper.Cyan("  • Multiple sheets: Summary, Drain Points, Vertices, Statistics"));
                    }
                }
            }

            data.Log(LogColorHelper.Cyan("===== AutoSlope Summary ====="));
            data.Log(LogColorHelper.Green($"Vertices Processed : {processed}"));
            data.Log(LogColorHelper.Yellow($"Vertices Skipped   : {skipped}"));
            data.Log(LogColorHelper.Cyan($"Highest Elevation  : {highest_mm:0} mm"));
            data.Log(LogColorHelper.Cyan($"Longest Path       : {longest_m:0.00} m"));
            data.Log(LogColorHelper.Cyan($"Drain Count        : {finalDrainPoints.Count}"));
            data.Log(LogColorHelper.Cyan($"Run Duration       : {durationSec} sec"));
            data.Log(LogColorHelper.Cyan($"Run Date           : {runDate}"));

            if (data.EnableDrainTolerance)
            {
                data.Log(LogColorHelper.Cyan($"Drain Tolerance    : {data.DrainToleranceMm} mm (enabled)"));
            }

            data.Log(LogColorHelper.Green("===== AutoSlope Finished Successfully ====="));
        }

        private static int FindNearestDrainIndex(XYZ vertexPos, List<XYZ> drainPoints)
        {
            if (drainPoints == null || drainPoints.Count == 0)
                return -1;

            int nearestIndex = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < drainPoints.Count; i++)
            {
                if (drainPoints[i] == null)
                    continue;

                double distance = vertexPos.DistanceTo(drainPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private static XYZ CalculateDirectionVector(XYZ fromPoint, XYZ toPoint)
        {
            if (fromPoint.DistanceTo(toPoint) < 0.001)
                return XYZ.Zero;

            XYZ vector = toPoint - fromPoint;
            return vector.Normalize();
        }
    }
}