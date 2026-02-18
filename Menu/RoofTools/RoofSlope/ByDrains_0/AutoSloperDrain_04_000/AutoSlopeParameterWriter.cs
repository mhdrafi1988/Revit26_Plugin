// =======================================================
// File: AutoSlopeParameterWriter.cs
// Location: Revit22_Plugin.Asd.Services
// =======================================================

using Autodesk.Revit.DB;
using System;

namespace Revit22_Plugin.Asd.Services
{
    public static class AutoSlopeParameterWriter
    {
        public static ParameterWriteResult WriteAll(
            Document doc,
            RoofBase roof,
            double slopePercentage,
            double highestElevation_mm,
            double longestPath_m,
            int verticesProcessed,
            int drainCount,
            int runDuration_sec,
            Action<string> logAction = null)
        {
            if (doc == null || roof == null)
                return new ParameterWriteResult();

            int successCount = 0;
            int failCount = 0;

            using (Transaction tx = new Transaction(doc, "AutoSlope – Update Roof Parameters"))
            {
                tx.Start();

                // Integer parameters
                TrySetInt(roof, "AutoSlope_HighestElevation",
                    (int)Math.Round(highestElevation_mm),
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_VerticesProcessed",
                    verticesProcessed,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_DrainCount",
                    drainCount,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_RunDuration_sec",
                    runDuration_sec,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_Status",
                    failCount > 0 ? 2 : 1,
                    ref successCount, ref failCount);

                // Double parameters
                TrySetDouble(roof, "AutoSlope_LongestPath",
                    longestPath_m,
                    ref successCount, ref failCount);

                TrySetDouble(roof, "AutoSlope_SlopePercent",
                    slopePercentage,
                    ref successCount, ref failCount);

                TrySetDouble(roof, "AutoSlope_Threshold",
                    100.0, // Default threshold in mm
                    ref successCount, ref failCount);

                // String parameters
                TrySetString(roof, "AutoSlope_RunDate",
                    DateTime.Now.ToString("dd-MMM-yyyy HH:mm"),
                    ref successCount, ref failCount);

                // Also update the standard Comments parameter for backward compatibility
                TrySetString(roof, "Comments",
                    $"Slope: {slopePercentage:F1}% | Max offset: {highestElevation_mm:F1}mm | Drains: {drainCount} | Date: {DateTime.Now:dd-MMM-yyyy HH:mm}",
                    ref successCount, ref failCount);

                tx.Commit();
            }

            logAction?.Invoke($"AutoSlope Parameters: {successCount} updated, {failCount} skipped/not found");

            return new ParameterWriteResult
            {
                SuccessCount = successCount,
                FailCount = failCount
            };
        }

        private static void TrySetInt(Element elem, string paramName, int value, ref int ok, ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null || p.IsReadOnly)
                {
                    fail++;
                    return;
                }

                // Check if parameter can accept integer
                if (p.StorageType == StorageType.Integer)
                {
                    p.Set(value);
                    ok++;
                }
                else if (p.StorageType == StorageType.Double)
                {
                    // Convert to double if needed
                    p.Set((double)value);
                    ok++;
                }
                else
                {
                    fail++;
                }
            }
            catch { fail++; }
        }

        private static void TrySetDouble(Element elem, string paramName, double value, ref int ok, ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null || p.IsReadOnly)
                {
                    fail++;
                    return;
                }

                if (p.StorageType == StorageType.Double)
                {
                    p.Set(value);
                    ok++;
                }
                else if (p.StorageType == StorageType.Integer)
                {
                    // Convert to integer if needed
                    p.Set((int)Math.Round(value));
                    ok++;
                }
                else
                {
                    fail++;
                }
            }
            catch { fail++; }
        }

        private static void TrySetString(Element elem, string paramName, string value, ref int ok, ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.String)
                {
                    fail++;
                    return;
                }
                p.Set(value);
                ok++;
            }
            catch { fail++; }
        }
    }

    public struct ParameterWriteResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public bool HasFailures => FailCount > 0;
    }
}