using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Services
{
    public static class CsvExportService
    {
        public static string GetDefaultExportFolder()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "AutoSlope_Reports");
        }

        public static string ExportResultsToCsv(
            string folderPath,
            Document document,
            ElementId roofId,
            AutoSlopeMetrics metrics,
            double slopePercent,
            int thresholdMeters,
            List<XYZ> drainPoints)
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(folderPath);

                // Generate filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"AutoSlope_Report_{timestamp}.csv";
                string filePath = Path.Combine(folderPath, fileName);

                // Get roof info
                RoofBase roof = document.GetElement(roofId) as RoofBase;
                string roofName = roof?.Name ?? "Unknown Roof";
                string roofIdStr = roofId.Value.ToString();

                // Prepare CSV content
                var csvLines = new List<string>
                {
                    "AutoSlope Report - Generated " + DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    "",
                    "PROJECT INFORMATION",
                    $"Project Name,{document.Title}",
                    $"Roof Element,{roofName} (ID: {roofIdStr})",
                    "",
                    "ANALYSIS PARAMETERS",
                    $"Slope Percentage,{slopePercent}%",
                    $"Threshold Distance,{thresholdMeters} m",
                    $"Drain Points Count,{drainPoints.Count}",
                    "",
                    "RESULTS",
                    $"Vertices Processed,{metrics.Processed}",
                    $"Vertices Skipped,{metrics.Skipped}",
                    $"Highest Elevation,{metrics.HighestElevation:F0} mm",
                    $"Longest Path,{metrics.LongestPath:F2} m",
                    "",
                    "DRAIN POINT COORDINATES (Project Units)",
                    "Index,X,Y,Z"
                };

                // Add drain point coordinates
                for (int i = 0; i < drainPoints.Count; i++)
                {
                    var point = drainPoints[i];
                    csvLines.Add($"{i + 1},{point.X:F3},{point.Y:F3},{point.Z:F3}");
                }

                // Write to file
                File.WriteAllLines(filePath, csvLines);

                return filePath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export CSV: {ex.Message}", ex);
            }
        }
    }
}