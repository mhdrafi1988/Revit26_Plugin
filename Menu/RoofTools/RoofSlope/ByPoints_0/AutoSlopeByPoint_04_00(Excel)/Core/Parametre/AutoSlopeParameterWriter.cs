// =======================================================
// File: AutoSlopeParameterWriter.cs
// Location: Core/Parameters/
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint_04.Core.Models;
using System;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Core.Parameters
{
    public static class AutoSlopeParameterWriter
    {
        public static void WriteAll(
    Document doc,
    RoofBase roof,
    AutoSlopePayload data,
    double highestElevation_mm,
    double longestPath_m,
    int processed,
    int skipped,
    int runDuration_sec,
    string version = "P.04.00") // ADD THIS PARAMETER WITH DEFAULT VALUE
        {
            if (doc == null || roof == null)
                return;

            int successCount = 0;
            int failCount = 0;

            using (Transaction tx = new Transaction(doc, "AutoSlope – Update Roof Parameters"))
            {
                tx.Start();

                TrySetInt(roof, "AutoSlope_HighestElevation",
                    (int)Math.Round(highestElevation_mm),
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_VerticesProcessed",
                    processed,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_VerticesSkipped",
                    skipped,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_DrainCount",
                    data.DrainPoints.Count,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_RunDuration_sec",
                    runDuration_sec,
                    ref successCount, ref failCount);

                TrySetDouble(roof, "AutoSlope_LongestPath",
                    longestPath_m,
                    ref successCount, ref failCount);

                TrySetDouble(roof, "AutoSlope_SlopePercent",
                    data.SlopePercent,
                    ref successCount, ref failCount);

                TrySetDouble(roof, "AutoSlope_Threshold",
                    data.ThresholdMeters * 1000.0,
                    ref successCount, ref failCount);

                TrySetString(roof, "AutoSlope_RunDate",
                    DateTime.Now.ToString("dd-MM-yy HH:mm"),
                    ref successCount, ref failCount);
                TrySetString(roof, "AutoSlope_RunDate",
                    DateTime.Now.ToString("dd-MM-yy HH:mm"),
                    ref successCount, ref failCount);

                TrySetString(roof, "AutoSlope_Versions",
                    version,
                    ref successCount, ref failCount);

                int statusValue = successCount == 0 ? 3 : failCount > 0 ? 2 : 1;
                TrySetInt(roof, "AutoSlope_Status", statusValue, ref successCount, ref failCount);

                tx.Commit();
            }

            data?.Log($"AutoSlope Parameters: {successCount} updated, {failCount} skipped");
        }

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
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.Integer)
                {
                    fail++;
                    return;
                }
                p.Set(value);
                ok++;
            }
            catch { fail++; }
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
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double)
                {
                    fail++;
                    return;
                }
                p.Set(value);
                ok++;
            }
            catch { fail++; }
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
}