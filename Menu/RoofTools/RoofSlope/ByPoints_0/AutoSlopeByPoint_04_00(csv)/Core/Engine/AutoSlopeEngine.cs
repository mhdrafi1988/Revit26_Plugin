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
            if (roof == null) return;

            SlabShapeEditor editor = roof.GetSlabShapeEditor();

            // Reset vertices
            using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
            {
                tx.Start();
                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                    editor.ModifySubElement(v, 0);
                tx.Commit();
            }

            List<SlabShapeVertex> vertices = new();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                vertices.Add(v);

            double slopeFactor = data.SlopePercent / 100.0;
            double thresholdFt = UnitUtils.ConvertToInternalUnits(data.ThresholdMeters, UnitTypeId.Meters);

            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log(LogColorHelper.Red("Top face not found. Aborting."));
                return;
            }

            var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFt);

            HashSet<int> drainIndices = new();
            for (int i = 0; i < vertices.Count; i++)
            {
                foreach (XYZ d in data.DrainPoints)
                {
                    if (vertices[i].Position.DistanceTo(d) < 0.5)
                    {
                        drainIndices.Add(i);
                        break;
                    }
                }
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
                                PathLengthMeters = UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
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
                    if (elevFt > maxElevFt) maxElevFt = elevFt;
                    if (pathFt > maxPathFt) maxPathFt = pathFt;

                    int nearestDrainIndex = FindNearestDrainIndex(vertices[i].Position, data.DrainPoints);
                    XYZ directionVector = CalculateDirectionVector(vertices[i].Position, data.DrainPoints[nearestDrainIndex]);

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
                2, MidpointRounding.AwayFromZero);

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
            data.Vm.DrainCount = data.DrainPoints.Count;
            data.Vm.RunDuration_sec = durationSec;
            data.Vm.RunDate = runDate;

            AutoSlopeParameterWriter.WriteAll(
                doc, roof, data, highest_mm, maxPathFt, processed, skipped, durationSec,"P.04.00");


            if (data.ExportConfig?.ExportToCsv == true)
            {
                string compactPath = CsvExportHelper.ExportCompactVertexData(
                    data, vertexDataList, roof, data.SlopePercent);

                if (!string.IsNullOrEmpty(compactPath))
                {
                    data.Log(LogColorHelper.Green($"? Compact vertex data exported to: {compactPath}"));
                    data.Log(LogColorHelper.Cyan($"  • Sorted by PathLength_Meters (longest first)"));
                    data.Log(LogColorHelper.Cyan($"  • Contains {processed} processed vertices"));

                    var longestVertex = vertexDataList
                        .Where(v => v.WasProcessed)
                        .OrderByDescending(v => v.PathLengthMeters)
                        .FirstOrDefault();

                    if (longestVertex != null)
                    {
                        data.Log(LogColorHelper.Cyan($"  • Longest path: {longestVertex.PathLengthMeters:F2} m to drain {longestVertex.NearestDrainIndex}"));
                    }
                }

                if (data.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = CsvExportHelper.ExportDetailedVertexData(
                        data, vertexDataList, roof, data.DrainPoints, data.SlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        data.Log(LogColorHelper.Green($"? Detailed vertex data exported to: {detailedPath}"));
                        data.Log(LogColorHelper.Cyan($"  • Contains {vertexDataList.Count} total vertices ({processed} processed, {skipped} skipped)"));
                    }
                }
            }

            data.Log(LogColorHelper.Cyan("===== AutoSlope Summary ====="));
            data.Log(LogColorHelper.Green($"Vertices Processed : {processed}"));
            data.Log(LogColorHelper.Yellow($"Vertices Skipped   : {skipped}"));
            data.Log(LogColorHelper.Cyan($"Highest Elevation  : {highest_mm:0} mm"));
            data.Log(LogColorHelper.Cyan($"Longest Path       : {longest_m:0.00} m"));
            data.Log(LogColorHelper.Cyan($"Drain Count        : {data.DrainPoints.Count}"));
            data.Log(LogColorHelper.Cyan($"Run Duration       : {durationSec} sec"));
            data.Log(LogColorHelper.Cyan($"Run Date           : {runDate}"));

            data.Log(LogColorHelper.Green("===== AutoSlope Finished ? ====="));
        }

        private static int FindNearestDrainIndex(XYZ vertexPos, List<XYZ> drainPoints)
        {
            int nearestIndex = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < drainPoints.Count; i++)
            {
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