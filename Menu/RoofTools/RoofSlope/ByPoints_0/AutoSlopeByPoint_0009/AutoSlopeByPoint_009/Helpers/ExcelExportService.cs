// =======================================================
// File: ExcelExportService.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V009
// Changes vs V06:
//   - payload.Log is now Action<LogEntry>; all log calls
//     emit new LogEntry(LogLevel.X, "...") instead of strings.
//   - ExportResultsSummary log parameter updated to
//     Action<LogEntry> for consistency.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.V009.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Revit26_Plugin.AutoSlopeByPoint.V009.Infrastructure.Helpers
{
    public static class ExcelExportService
    {
        private static bool? _isAvailable = null;

        private static bool IsAvailable(Action<LogEntry> log)
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;

            string pluginFolder = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            string epPlusPath = Path.Combine(pluginFolder, "EPPlus.dll");

            _isAvailable = File.Exists(epPlusPath);

            if (!_isAvailable.Value)
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    $"⚠ Excel export skipped: EPPlus.dll not found in plugin folder. Expected at: {epPlusPath}"));

            return _isAvailable.Value;
        }

        public static string ExportCompactVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            double slopePercent)
        {
            if (!IsAvailable(payload?.Log)) return null;
            try
            {
                return ExcelExportHelper.ExportCompactVertexData(
                    payload, vertexData, roof, slopePercent);
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke(new LogEntry(LogLevel.Error,
                    $"⚠ Compact Excel export error: {ex.Message}"));
                return null;
            }
        }

        public static string ExportDetailedVertexData(
            AutoSlopePayload payload,
            List<VertexData> vertexData,
            RoofBase roof,
            List<XYZ> drainPoints,
            double slopePercent)
        {
            if (!IsAvailable(payload?.Log)) return null;
            try
            {
                return ExcelExportHelper.ExportDetailedVertexData(
                    payload, vertexData, roof, drainPoints, slopePercent);
            }
            catch (Exception ex)
            {
                payload?.Log?.Invoke(new LogEntry(LogLevel.Error,
                    $"⚠ Detailed Excel export error: {ex.Message}"));
                return null;
            }
        }

        public static string ExportResultsSummary(
            string filePath,
            AutoSlopeResult result,
            double slopePercent,
            int thresholdMeters,
            bool enableDrainTolerance,
            int drainToleranceMm,
            string exportFolderPath,
            Action<LogEntry> log = null)
        {
            if (!IsAvailable(log)) return null;
            try
            {
                return ExcelExportHelper.ExportResultsSummary(
                    filePath, result, slopePercent,
                    thresholdMeters, enableDrainTolerance,
                    drainToleranceMm, exportFolderPath);
            }
            catch (Exception ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Error,
                    $"⚠ Results Excel export error: {ex.Message}"));
                return null;
            }
        }
    }
}
