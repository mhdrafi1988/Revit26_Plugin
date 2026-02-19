// File: CsvExportHelper.cs
// Location: Revit26_Plugin.Asd_19.Services

using Autodesk.Revit.DB;
using Revit26_Plugin.Asd_19.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.Asd_19.Services
{
    public static class CsvExportHelper
    {
        /// <summary>
        /// Export detailed vertex data including all vertices with full information
        /// </summary>
        public static string ExportDetailedVertexData(
            ExportConfig config,
            List<DrainVertexData> vertexData,
            RoofBase roof,
            List<DrainItem> drains,
            double slopePercent,
            Action<string> logAction = null)
        {
            if (config == null || !config.ExportToCsv || vertexData == null)
                return null;

            try
            {
                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}_DETAILED.csv";
                string filePath = Path.Combine(config.ExportPath, fileName);

                var csvLines = new List<string>
                {
                    "DRAIN DETECTION DETAILED VERTEX EXPORT",
                    $"Generated: {DateTime.Now:dd-MM-yyyy HH:mm:ss}",
                    $"Revit Document: {roof.Document.Title}",
                    $"Roof ElementId: {roof.Id.Value}",
                    $"Roof Name: {roof.Name}",
                    $"Slope Percentage: {slopePercent}%",
                    $"Total Vertices: {vertexData.Count}",
                    $"Processed Vertices: {vertexData.Count(v => v.WasProcessed)}",
                    $"Skipped Vertices: {vertexData.Count(v => !v.WasProcessed)}",
                    $"Drain Points Count: {drains.Count}",
                    ""
                };

                // Add drain points information
                csvLines.Add("DRAIN POINTS");
                csvLines.Add("DrainId,Size (mm),Shape,X (m),Y (m),Z (m)");
                for (int i = 0; i < drains.Count; i++)
                {
                    var drain = drains[i];
                    csvLines.Add($"{i + 1}," +
                                 $"{drain.SizeCategory}," +
                                 $"{drain.ShapeType}," +
                                 $"{FormatDouble(drain.CenterPoint.X)}," +
                                 $"{FormatDouble(drain.CenterPoint.Y)}," +
                                 $"{FormatDouble(drain.CenterPoint.Z)}");
                }
                csvLines.Add("");

                // Add vertex details
                csvLines.Add("VERTEX DETAILS");
                csvLines.Add("VertexIndex,PathLength_Meters,SlopePercent,ElevationOffset_mm," +
                            "NearestDrainId,DrainSize,DrainShape,Direction,WasProcessed," +
                            "Position_X,Position_Y,Position_Z");

                var sortedVertices = vertexData
                    .Where(v => v.WasProcessed)
                    .OrderByDescending(v => v.PathLengthMeters)
                    .Concat(vertexData.Where(v => !v.WasProcessed))
                    .ToList();

                foreach (var vertex in sortedVertices)
                {
                    string line = $"{vertex.VertexIndex}," +
                                 $"{vertex.PathLengthMeters:F2}," +
                                 $"{slopePercent:F2}," +
                                 $"{vertex.ElevationOffsetMm:0}," +
                                 $"{vertex.NearestDrainId}," +
                                 $"\"{vertex.DrainSize}\"," +
                                 $"{vertex.DrainShape}," +
                                 $"\"{vertex.Direction}\"," +
                                 $"{(vertex.WasProcessed ? "YES" : "NO")}," +
                                 $"{FormatDouble(vertex.Position.X)}," +
                                 $"{FormatDouble(vertex.Position.Y)}," +
                                 $"{FormatDouble(vertex.Position.Z)}";
                    csvLines.Add(line);
                }
                csvLines.Add("");

                // Add summary statistics
                csvLines.Add("SUMMARY STATISTICS");
                csvLines.Add("Metric,Value,Unit");
                csvLines.Add($"Total Vertices,{vertexData.Count},count");
                csvLines.Add($"Processed Vertices,{vertexData.Count(v => v.WasProcessed)},count");
                csvLines.Add($"Skipped Vertices,{vertexData.Count(v => !v.WasProcessed)},count");

                if (vertexData.Any(v => v.WasProcessed))
                {
                    csvLines.Add($"Average Path Length,{vertexData.Where(v => v.WasProcessed).Average(v => v.PathLengthMeters):F2},meters");
                    csvLines.Add($"Maximum Path Length,{vertexData.Where(v => v.WasProcessed).Max(v => v.PathLengthMeters):F2},meters");
                    csvLines.Add($"Minimum Path Length,{vertexData.Where(v => v.WasProcessed).Min(v => v.PathLengthMeters):F2},meters");
                    csvLines.Add($"Average Elevation Offset,{vertexData.Where(v => v.WasProcessed).Average(v => v.ElevationOffsetMm):0},mm");
                    csvLines.Add($"Maximum Elevation Offset,{vertexData.Where(v => v.WasProcessed).Max(v => v.ElevationOffsetMm):0},mm");
                }

                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                logAction?.Invoke($"✓ Detailed CSV exported to: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"✗ Detailed CSV Export Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Export compact vertex data (only processed vertices with essential info)
        /// </summary>
        public static string ExportCompactVertexData(
            ExportConfig config,
            List<DrainVertexData> vertexData,
            RoofBase roof,
            double slopePercent,
            Action<string> logAction = null)
        {
            if (config == null || !config.ExportToCsv || vertexData == null)
                return null;

            try
            {
                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}.csv";
                string filePath = Path.Combine(config.ExportPath, fileName);

                var csvLines = new List<string>
                {
                    "VertexIndex,PathLength_Meters,SlopePercent,ElevationOffset_mm,NearestDrainId,DrainSize,DrainShape,Direction"
                };

                var sortedVertices = vertexData
                    .Where(v => v.WasProcessed)
                    .OrderByDescending(v => v.PathLengthMeters)
                    .ToList();

                foreach (var vertex in sortedVertices)
                {
                    string line = $"{vertex.VertexIndex}," +
                                 $"{vertex.PathLengthMeters:F2}," +
                                 $"{slopePercent:F2}," +
                                 $"{vertex.ElevationOffsetMm:0}," +
                                 $"{vertex.NearestDrainId}," +
                                 $"\"{vertex.DrainSize}\"," +
                                 $"{vertex.DrainShape}," +
                                 $"\"{vertex.Direction}\"";
                    csvLines.Add(line);
                }

                csvLines.Add("");
                csvLines.Add("SUMMARY,Sorted by PathLength_Meters (descending)");
                csvLines.Add($"Total Processed Vertices,{sortedVertices.Count}");

                if (sortedVertices.Any())
                {
                    csvLines.Add($"Longest Path,{sortedVertices.First().PathLengthMeters:F2} m");
                    csvLines.Add($"Shortest Path,{sortedVertices.Last().PathLengthMeters:F2} m");
                }

                File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
                logAction?.Invoke($"✓ Compact CSV exported to: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"✗ Compact CSV Export Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Export summary only - UPDATED to match Part 01 naming convention
        /// </summary>
        public static string ExportSummaryOnly(
            ExportConfig config,
            DrainExportMetrics metrics,
            RoofBase roof,
            double slopePercent,
            Action<string> logAction = null)
        {
            if (config == null || !config.ExportToCsv)
                return null;

            try
            {
                string roofId = roof.Id.Value.ToString();
                string slopeStr = slopePercent.ToString("0.00", CultureInfo.InvariantCulture);
                string dateStr = DateTime.Now.ToString("dd-MM-yy");
                string fileName = $"{roofId}_{slopeStr}_{dateStr}_SUMMARY.csv";
                string filePath = Path.Combine(config.ExportPath, fileName);

                var lines = new[]
                {
                    "Parameter,Value,Unit",
                    $"Project Name,{roof.Document.Title},",
                    $"Roof ElementId,{roof.Id.Value},",
                    $"Roof Name,{roof.Name},",
                    $"Run Date,{metrics.RunDate},",
                    $"Slope Percentage,{metrics.SlopePercent},%",
                    $"Vertices Processed,{metrics.ProcessedVertices},count",
                    $"Vertices Skipped,{metrics.SkippedVertices},count",
                    $"Drain Count,{metrics.DrainCount},count",
                    $"Highest Elevation,{metrics.HighestElevationMm:0},mm",
                    $"Longest Path,{metrics.LongestPathM:0.00},meters",
                    $"Run Duration,{metrics.RunDurationSec},seconds"
                };

                File.WriteAllLines(filePath, lines, Encoding.UTF8);
                logAction?.Invoke($"✓ Summary CSV exported to: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"✗ Summary Export Error: {ex.Message}");
                return null;
            }
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }
}