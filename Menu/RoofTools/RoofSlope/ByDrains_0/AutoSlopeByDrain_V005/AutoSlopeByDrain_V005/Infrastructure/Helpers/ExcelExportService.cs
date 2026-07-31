// File: ExcelExportService.cs
// Location: Infrastructure/Helpers/
// Ported from AutoSlopeByPoint's ExcelExportService pattern — checks
// EPPlus.dll is actually present in the plugin folder before attempting to
// use it (so a missing dependency logs a clear Warning instead of an
// unhandled exception), then delegates to ExcelExportHelper.

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain.V005.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Revit26_Plugin.AutoSlopeByDrain.V005.Infrastructure.Helpers
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

        public static string ExportWorkbook(
            AutoSlopeDrainPayload payload,
            List<DrainVertexData> vertexData,
            RoofBase roof,
            double slopePercent,
            DrainExportMetrics metrics,
            Action<LogEntry> log = null)
        {
            if (!IsAvailable(log ?? payload?.Log)) return null;
            try
            {
                return ExcelExportHelper.ExportWorkbook(
                    payload, vertexData, roof, slopePercent, metrics, metrics?.Version ?? "006");
            }
            catch (Exception ex)
            {
                (log ?? payload?.Log)?.Invoke(new LogEntry(LogLevel.Error,
                    $"⚠ Excel export error: {ex.Message}"));
                return null;
            }
        }
    }
}
