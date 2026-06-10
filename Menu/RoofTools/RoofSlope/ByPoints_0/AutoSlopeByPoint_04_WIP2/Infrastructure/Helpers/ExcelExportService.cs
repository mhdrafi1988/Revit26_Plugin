// =======================================================
// File: ExcelExportService.cs
// Purpose:
//   Isolation wrapper around ExcelExportHelper.
//   Checks whether EPPlus.dll is present on disk BEFORE
//   attempting any call into ExcelExportHelper.
//
//   Why this is needed:
//   ExcelExportHelper references OfficeOpenXml types
//   (EPPlus). If EPPlus.dll is missing, the CLR throws a
//   FileNotFoundException the moment it tries to JIT any
//   method that touches that class — BEFORE any try/catch
//   inside the method can run. This wrapper is the only
//   class AutoSlopeEngine and AutoSlopeViewModel call.
//   ExcelExportHelper is never touched directly from
//   outside this file.
//
//   How it works:
//   1. On first call, _isAvailable is resolved once via
//      File.Exists on "EPPlus.dll" next to this plugin's
//      DLL. The result is cached — no repeated disk checks.
//   2. If EPPlus.dll is missing, a single clear log message
//      is written and null is returned. The Revit operation
//      continues normally.
//   3. If EPPlus.dll is present, the call is forwarded to
//      ExcelExportHelper inside a try/catch, so any runtime
//      error (bad path, locked file, etc.) is also isolated.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers
{
    public static class ExcelExportService
    {
        // ── EPPlus availability (resolved once, then cached) ─────────────────
        private static bool? _isAvailable = null;

        private static bool IsAvailable(Action<string> log)
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;

            // Look for EPPlus.dll in the same folder as this plugin's DLL.
            // That is where NuGet places it after the build copies output.
            string pluginFolder = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location) ?? string.Empty;

            string epPlusPath = Path.Combine(pluginFolder, "EPPlus.dll");

            _isAvailable = File.Exists(epPlusPath);

            if (!_isAvailable.Value)
            {
                log?.Invoke(
                    $"⚠ Excel export skipped: EPPlus.dll not found in plugin folder." +
                    $" Expected at: {epPlusPath}");
            }

            return _isAvailable.Value;
        }

        // ── Public surface (mirrors the three methods used in the project) ───

        /// <summary>
        /// Exports a compact per-vertex sheet.
        /// Returns the saved file path, or null if EPPlus is unavailable or
        /// an error occurs.
        /// </summary>
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
                payload?.Log?.Invoke(
                    $"⚠ Compact Excel export error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Exports a detailed multi-sheet workbook (Summary, Drain Points,
        /// Vertices, Statistics).
        /// Returns the saved file path, or null if EPPlus is unavailable or
        /// an error occurs.
        /// </summary>
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
                payload?.Log?.Invoke(
                    $"⚠ Detailed Excel export error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Exports a single summary sheet to a user-chosen file path.
        /// Returns the saved file path, or null if EPPlus is unavailable or
        /// an error occurs.
        /// </summary>
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