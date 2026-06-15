// =======================================================
// File: ExcelExportService.cs
// MIGRATION: EPPlus → ClosedXML
//
// Only change from the original:
//   DLL check updated from "EPPlus.dll" → "ClosedXML.dll"
//   Log message updated to match.
//
// Everything else (isolation pattern, caching, method
// signatures) is identical.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint_04_01.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Revit26_Plugin.AutoSlopeByPoint_04_01.Infrastructure.Helpers
{
    public static class ExcelExportService
    {
        // ── ClosedXML availability (resolved once, then cached) ──────────────
        private static bool? _isAvailable = null;

        private static bool IsAvailable(Action<string> log)
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;

            string pluginFolder = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location) ?? string.Empty;

            // ← changed from EPPlus.dll
            string dllPath = Path.Combine(pluginFolder, "ClosedXML.dll");

            _isAvailable = File.Exists(dllPath);

            if (!_isAvailable.Value)
            {
                log?.Invoke(
                    $"⚠ Excel export skipped: ClosedXML.dll not found in plugin folder." +
                    $" Expected at: {dllPath}");
            }

            return _isAvailable.Value;
        }

        // ── Public surface (mirrors the three methods used in the project) ───

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
                payload?.Log?.Invoke($"⚠ Compact Excel export error: {ex.Message}");
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
                payload?.Log?.Invoke($"⚠ Detailed Excel export error: {ex.Message}");
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
            Action<string> log = null)
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
                log?.Invoke($"⚠ Results Excel export error: {ex.Message}");
                return null;
            }
        }
    }
}