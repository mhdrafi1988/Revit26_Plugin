// =======================================================
// File: AutoSlopeParameterWriter.cs
// Purpose: Fault-tolerant roof parameter updates
// Revit: 2026
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.Models;
using System;

namespace Revit26_Plugin.AutoSlopeByPoint.Parameters
{
    /// <summary>
    /// Writes AutoSlope parameters to the roof.
    /// Each parameter is handled independently:
    /// - Missing / invalid parameters are skipped
    /// - One failure NEVER blocks others
    /// - Transaction always commits
    /// </summary>
    public static class AutoSlopeParameterWriter
    {
        public static void WriteAll(
            Document doc,
            RoofBase roof,
            AutoSlopePayload data,
            int highestElevation_mm,
            double averageElevation_ft,   // INTERNAL UNITS (feet)
            double longestPath_ft,
            int processed,
            int skipped,
            int runDuration_sec)
        {
            if (doc == null || roof == null)
                return;

            int successCount = 0;
            int failCount = 0;

            using (Transaction tx =
                new Transaction(doc, "AutoSlope – Update Roof Parameters"))
            {
                tx.Start();

                // --------------------------------------------------
                // INTEGER PARAMETERS
                // --------------------------------------------------
                TrySetInt(
                    roof,
                    "AutoSlope_HighestElevation_mm",
                    highestElevation_mm,
                    ref successCount,
                    ref failCount);

                TrySetInt(
                    roof,
                    "AutoSlope_VerticesProcessed",
                    processed,
                    ref successCount,
                    ref failCount);

                TrySetInt(
                    roof,
                    "AutoSlope_VerticesSkipped",
                    skipped,
                    ref successCount,
                    ref failCount);

                TrySetInt(
                    roof,
                    "AutoSlope_DrainCount",
                    data?.DrainPoints?.Count ?? 0,
                    ref successCount,
                    ref failCount);

                TrySetInt(
                    roof,
                    "AutoSlope_RunDuration_sec",
                    runDuration_sec,
                    ref successCount,
                    ref failCount);

                // --------------------------------------------------
                // DOUBLE PARAMETERS
                // --------------------------------------------------
                TrySetDouble(
                    roof,
                    "AutoSlope_AverageElevation_ft", // NEW (internal units)
                    averageElevation_ft,
                    ref successCount,
                    ref failCount);

                TrySetDouble(
                    roof,
                    "AutoSlope_LongestPath_ft",
                    longestPath_ft,
                    ref successCount,
                    ref failCount);

                TrySetDouble(
                    roof,
                    "AutoSlope_SlopePercent",
                    data?.SlopePercent ?? 0.0,
                    ref successCount,
                    ref failCount);

                // Threshold stored as mm (double for historical compatibility)
                TrySetDouble(
                    roof,
                    "AutoSlope_Threshold_mm",
                    (data?.ThresholdMeters ?? 0.0) * 1000.0,
                    ref successCount,
                    ref failCount);

                // --------------------------------------------------
                // STRING PARAMETERS
                // --------------------------------------------------
                TrySetString(
                    roof,
                    "AutoSlope_RunDate",
                    DateTime.Now.ToString("dd-MM-yy HH:mm"),
                    ref successCount,
                    ref failCount);

                // --------------------------------------------------
                // STATUS PARAMETER
                // 1 = OK, 2 = PARTIAL, 3 = FAILED
                // --------------------------------------------------
                int statusValue =
                    successCount == 0 ? 3 :
                    failCount > 0 ? 2 : 1;

                TrySetInt(
                    roof,
                    "AutoSlope_Status",
                    statusValue,
                    ref successCount,
                    ref failCount);

                tx.Commit();
            }

            // Optional UI logging (safe, non-blocking)
            data?.Log?.Invoke(
                $"AutoSlope Parameters: {successCount} updated, {failCount} skipped");
        }

        // ======================================================
        // SAFE PARAMETER SETTERS
        // ======================================================
        private static void TrySetInt(
            Element elem,
            string paramName,
            int value,
            ref int ok,
            ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null ||
                    p.IsReadOnly ||
                    p.StorageType != StorageType.Integer)
                {
                    fail++;
                    return;
                }

                p.Set(value);
                ok++;
            }
            catch
            {
                fail++;
            }
        }

        private static void TrySetDouble(
            Element elem,
            string paramName,
            double value,
            ref int ok,
            ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null ||
                    p.IsReadOnly ||
                    p.StorageType != StorageType.Double)
                {
                    fail++;
                    return;
                }

                p.Set(value);
                ok++;
            }
            catch
            {
                fail++;
            }
        }

        private static void TrySetString(
            Element elem,
            string paramName,
            string value,
            ref int ok,
            ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null ||
                    p.IsReadOnly ||
                    p.StorageType != StorageType.String)
                {
                    fail++;
                    return;
                }

                p.Set(value);
                ok++;
            }
            catch
            {
                fail++;
            }
        }
    }
}
