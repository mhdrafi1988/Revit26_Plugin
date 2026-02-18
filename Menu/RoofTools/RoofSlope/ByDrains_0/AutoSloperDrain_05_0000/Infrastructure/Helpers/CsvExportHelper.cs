using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.AutoSlope.V5_00.Infrastructure.Helpers
{
    public static class CsvExportHelper
    {
        public static string ExportCompactData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            double slopePercent)
        {
            if (payload?.ExportConfig == null || !payload.ExportConfig.ExportToCsv || vertexData == null)
                return null;

            try
            {
                // Ensure directory exists
                if (!Directory.Exists(payload.ExportConfig.ExportPath))
                {
                    Directory.CreateDirectory(payload.ExportConfig.ExportPath);
                }

                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}.csv";
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                var csvLines = new List<string>
                {
                    "VertexIndex,PathLength_Meters,SlopePercent,ElevationOffset_mm,NearestDrainId,WasProcessed"
                };

                var sortedData = vertexData
                    .Where(v => v.WasProcessed)
                    .OrderByDescending(v => v.PathLengthMeters)
                    .ToList();

                foreach (var v in sortedData)
                {
                    csvLines.Add($"{v.VertexIndex}," +
                                $"{v.PathLengthMeters:F3}," +
                                $"{slopePercent:F2}," +
                                $"{v.ElevationOffsetMm:F0}," +
                                $"{v.NearestDrainId}," +
                                $"{(v.WasProcessed ? "YES" : "NO")}");
                }

                csvLines.Add("");
                csvLines.Add($"# Total Processed: {sortedData.Count}");
                csvLines.Add($"# Generated: {DateTime.Now:dd-MMM-yyyy HH:mm:ss}");
                csvLines.Add($"# Roof ID: {roof.Id.Value}");
                csvLines.Add($"# Slope: {slopePercent}%");

                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"CSV Export Error: {ex.Message}");
                return null;
            }
        }

        public static string ExportDetailedData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            List<DrainItem> drains,
            double slopePercent)
        {
            if (payload?.ExportConfig == null || !payload.ExportConfig.ExportToCsv || vertexData == null)
                return null;

            try
            {
                // Ensure directory exists
                if (!Directory.Exists(payload.ExportConfig.ExportPath))
                {
                    Directory.CreateDirectory(payload.ExportConfig.ExportPath);
                }

                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}_DETAILED.csv";
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                var csvLines = new List<string>
                {
                    "AUTOSLOPE V5 - DETAILED EXPORT",
                    $"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm:ss}",
                    $"Revit Document: {roof.Document.Title}",
                    $"Roof ElementId: {roof.Id.Value}",
                    $"Slope Percentage: {slopePercent}%",
                    $"Threshold Distance: {payload.ThresholdMeters} meters",
                    $"Total Vertices: {vertexData.Count}",
                    $"Processed Vertices: {vertexData.Count(v => v.WasProcessed)}",
                    $"Skipped Vertices: {vertexData.Count(v => !v.WasProcessed)}",
                    $"Drain Points Count: {drains.Count}",
                    ""
                };

                csvLines.Add("DRAIN POINTS");
                csvLines.Add("DrainId,X (m),Y (m),Z (m),Width (mm),Height (mm),Shape");

                foreach (var d in drains)
                {
                    csvLines.Add($"{d.DrainId}," +
                                $"{FormatDouble(d.CenterPoint.X)}," +
                                $"{FormatDouble(d.CenterPoint.Y)}," +
                                $"{FormatDouble(d.CenterPoint.Z)}," +
                                $"{d.Width:F0}," +
                                $"{d.Height:F0}," +
                                $"{d.ShapeType}");
                }

                csvLines.Add("");
                csvLines.Add("VERTEX DETAILS");
                csvLines.Add("VertexIndex,PathLength_Meters,ElevationOffset_mm,NearestDrainId,Direction,WasProcessed,X,Y,Z");

                var sortedData = vertexData.OrderByDescending(v => v.PathLengthMeters).ToList();

                foreach (var v in sortedData)
                {
                    csvLines.Add($"{v.VertexIndex}," +
                                $"{v.PathLengthMeters:F3}," +
                                $"{v.ElevationOffsetMm:F0}," +
                                $"{v.NearestDrainId}," +
                                $"{v.Direction}," +
                                $"{(v.WasProcessed ? "YES" : "NO")}," +
                                $"{FormatDouble(v.Position.X)}," +
                                $"{FormatDouble(v.Position.Y)}," +
                                $"{FormatDouble(v.Position.Z)}");
                }

                csvLines.Add("");
                csvLines.Add("SUMMARY STATISTICS");
                csvLines.Add("Metric,Value,Unit");

                var processed = vertexData.Where(v => v.WasProcessed).ToList();
                csvLines.Add($"Total Vertices,{vertexData.Count},count");
                csvLines.Add($"Processed Vertices,{processed.Count},count");
                csvLines.Add($"Skipped Vertices,{vertexData.Count(v => !v.WasProcessed)},count");

                if (processed.Any())
                {
                    csvLines.Add($"Average Path Length,{processed.Average(v => v.PathLengthMeters):F2},meters");
                    csvLines.Add($"Maximum Path Length,{processed.Max(v => v.PathLengthMeters):F2},meters");
                    csvLines.Add($"Minimum Path Length,{processed.Min(v => v.PathLengthMeters):F2},meters");
                    csvLines.Add($"Average Elevation Offset,{processed.Average(v => v.ElevationOffsetMm):F0},mm");
                    csvLines.Add($"Maximum Elevation Offset,{processed.Max(v => v.ElevationOffsetMm):F0},mm");
                }

                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                return filePath;
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke($"Detailed CSV Export Error: {ex.Message}");
                return null;
            }
        }

        public static string ExportSummaryOnly(
            AutoSlopePayload payload,
            AutoSlopeMetrics metrics,
            RoofBase roof)
        {
            if (payload?.ExportConfig == null)
                return null;

            try
            {
                // Ensure directory exists
                if (!Directory.Exists(payload.ExportConfig.ExportPath))
                {
                    Directory.CreateDirectory(payload.ExportConfig.ExportPath);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{payload.ExportConfig.FileNamePrefix}_Summary_{timestamp}.csv";
                string filePath = Path.Combine(payload.ExportConfig.ExportPath, fileName);

                string projectName = payload.Vm?.UIDoc?.Document?.Title ?? "Unknown Project";

                var lines = new[]
                {
                    "Parameter,Value,Unit",
                    $"Project Name,{projectName},",
                    $"Roof ElementId,{payload.RoofId.Value},",
                    $"Run Date,{DateTime.Now:dd-MMM-yyyy HH:mm:ss},",
                    $"Slope Percentage,{payload.SlopePercent},%",
                    $"Threshold Distance,{payload.ThresholdMeters},meters",
                    $"Vertices Processed,{metrics.Processed},count",
                    $"Vertices Skipped,{metrics.Skipped},count",
                    $"Drain Points,{payload.SelectedDrains.Count},count",
                    $"Highest Elevation,{metrics.HighestElevation:F0},mm",
                    $"Longest Path,{metrics.LongestPath:F2},meters",
                    $"Run Duration,{metrics.DurationSeconds},seconds"
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