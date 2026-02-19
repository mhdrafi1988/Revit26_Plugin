using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers
{
    public static class CsvExportHelper
    {
        public static string ExportDetailedVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            List<XYZ> drainPoints,
            double slopePercent)
        {
            if (payload?.ExportConfig == null || !payload.ExportConfig.ExportToCsv || vertexData == null)
                return null;

            try
            {
                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}_DETAILED.csv";
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                var csvLines = new List<string>
                {
                    "AUTOSLOPE DETAILED VERTEX EXPORT",
                    $"Generated: {DateTime.Now:dd-MM-yyyy HH:mm:ss}",
                    $"Revit Document: {roof.Document.Title}",
                    $"Roof ElementId: {roof.Id.Value}",
                    $"Slope Percentage: {slopePercent}%",
                    $"Threshold Distance: {payload.ThresholdMeters} meters",
                    $"Total Vertices: {vertexData.Count}",
                    $"Processed Vertices: {vertexData.Count(v => v.WasProcessed)}",
                    $"Skipped Vertices: {vertexData.Count(v => !v.WasProcessed)}",
                    $"Drain Points Count: {drainPoints.Count}",
                    ""
                };

                csvLines.Add("DRAIN POINTS");
                csvLines.Add("DrainIndex,X (m),Y (m),Z (m)");
                for (int i = 0; i < drainPoints.Count; i++)
                {
                    var point = drainPoints[i];
                    csvLines.Add($"{i},{FormatDouble(point.X)},{FormatDouble(point.Y)},{FormatDouble(point.Z)}");
                }
                csvLines.Add("");

                csvLines.Add("VERTEX DETAILS");
                csvLines.Add("RoofElementId,DrainElementId,PointIndex,PathLength_Meters,SlopePercent,ElevationOffset_mm,Direction,WasProcessed,Position_X,Position_Y,Position_Z");

                var sortedVertices = vertexData
                    .Where(v => v.WasProcessed)
                    .OrderByDescending(v => v.PathLengthMeters)
                    .Concat(vertexData.Where(v => !v.WasProcessed))
                    .ToList();

                foreach (var vertex in sortedVertices)
                {
                    string line = $"{roof.Id.Value}," +
                                 $"{vertex.NearestDrainIndex}," +
                                 $"{vertex.VertexIndex}," +
                                 $"{vertex.PathLengthMeters:F2}," +
                                 $"{slopePercent:F2}," +
                                 $"{vertex.ElevationOffsetMm:0}," +
                                 $"{vertex.Direction}," +
                                 $"{(vertex.WasProcessed ? "YES" : "NO")}," +
                                 $"{FormatDouble(vertex.Position.X)}," +
                                 $"{FormatDouble(vertex.Position.Y)}," +
                                 $"{FormatDouble(vertex.Position.Z)}";
                    csvLines.Add(line);
                }
                csvLines.Add("");

                csvLines.Add("SUMMARY STATISTICS");
                csvLines.Add("Metric,Value,Unit");
                csvLines.Add($"Total Vertices,{vertexData.Count},count");
                csvLines.Add($"Processed Vertices,{vertexData.Count(v => v.WasProcessed)},count");
                csvLines.Add($"Skipped Vertices,{vertexData.Count(v => !v.WasProcessed)},count");
                csvLines.Add($"Average Path Length,{vertexData.Where(v => v.WasProcessed).Average(v => v.PathLengthMeters):F2},meters");
                csvLines.Add($"Maximum Path Length,{vertexData.Where(v => v.WasProcessed).Max(v => v.PathLengthMeters):F2},meters");
                csvLines.Add($"Minimum Path Length,{vertexData.Where(v => v.WasProcessed).Min(v => v.PathLengthMeters):F2},meters");
                csvLines.Add($"Average Elevation Offset,{vertexData.Where(v => v.WasProcessed).Average(v => v.ElevationOffsetMm):0},mm");
                csvLines.Add($"Maximum Elevation Offset,{vertexData.Where(v => v.WasProcessed).Max(v => v.ElevationOffsetMm):0},mm");

                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"Detailed CSV Export Error: {ex.Message}");
                return null;
            }
        }

        public static string ExportCompactVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            double slopePercent)
        {
            if (payload?.ExportConfig == null || !payload.ExportConfig.ExportToCsv || vertexData == null)
                return null;

            try
            {
                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}.csv";
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                var csvLines = new List<string>
                {
                    "RoofElementId,DrainElementId,PointIndex,PathLength_Meters,SlopePercent,ElevationOffset_mm,Direction"
                };

                var sortedVertices = vertexData
                    .Where(v => v.WasProcessed)
                    .OrderByDescending(v => v.PathLengthMeters)
                    .ToList();

                foreach (var vertex in sortedVertices)
                {
                    string line = $"{roof.Id.Value}," +
                                 $"{vertex.NearestDrainIndex}," +
                                 $"{vertex.VertexIndex}," +
                                 $"{vertex.PathLengthMeters:F2}," +
                                 $"{slopePercent:F2}," +
                                 $"{vertex.ElevationOffsetMm:0}," +
                                 $"{vertex.Direction}";
                    csvLines.Add(line);
                }

                csvLines.Add("");
                csvLines.Add("SUMMARY,Sorted by PathLength_Meters (descending)");
                csvLines.Add($"Total Processed Vertices,{sortedVertices.Count}");
                csvLines.Add($"Longest Path,{sortedVertices.First().PathLengthMeters:F2} m");
                csvLines.Add($"Shortest Path,{sortedVertices.Last().PathLengthMeters:F2} m");

                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"Compact CSV Export Error: {ex.Message}");
                return null;
            }
        }

        public static string ExportSummaryOnly(AutoSlopePayload payload, AutoSlopeMetrics metrics)
        {
            if (payload?.ExportConfig == null)
                return null;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{payload.ExportConfig.FileNamePrefix}_Summary_{timestamp}.csv";
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                string projectName = payload.Vm?.UIDoc?.Document?.Title ?? "Unknown Project";

                var lines = new[]
                {
                    "Parameter,Value,Unit",
                    $"Project Name,{projectName},",
                    $"Roof ElementId,{payload.RoofId.Value},",
                    $"Run Date,{DateTime.Now:dd-MM-yyyy HH:mm:ss},",
                    $"Slope Percentage,{payload.SlopePercent},%",
                    $"Threshold Distance,{payload.ThresholdMeters},meters",
                    $"Vertices Processed,{metrics.Processed},count",
                    $"Vertices Skipped,{metrics.Skipped},count",
                    $"Drain Points,{payload.DrainPoints.Count},count",
                    $"Highest Elevation,{metrics.HighestElevation:0},mm",
                    $"Longest Path,{metrics.LongestPath:0.00},meters"
                };

                File.WriteAllLines(filePath, lines, Encoding.UTF8);
                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"Summary Export Error: {ex.Message}");
                return null;
            }
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }
}