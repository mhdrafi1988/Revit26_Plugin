using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Export
{
    public static class AutoSlopeCsvExporter
    {
        public static void Export(
            List<AutoSlopeVertexExportDto> rows,
            string folderPath)
        {
            if (rows == null || rows.Count == 0)
                return;

            Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(
                folderPath,
                $"AutoSlope_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            var sb = new StringBuilder();

            // HEADER — exact order
            sb.AppendLine(
                "RoofElementId," +
                "DrainElementId," +
                "PointIndex," +
                "PathLength," +
                "SlopePercent," +
                "ElevationOffset," +
                "Direction");

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    r.RoofElementId,
                    r.DrainElementId,
                    r.PointIndex,
                    r.PathLength.ToString("F3", CultureInfo.InvariantCulture),
                    r.SlopePercent.ToString("F2", CultureInfo.InvariantCulture),
                    r.ElevationOffset.ToString("F3", CultureInfo.InvariantCulture),
                    r.Direction));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}