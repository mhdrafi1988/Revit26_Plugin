using Autodesk.Revit.DB;
using AutoSlopeByPointTwoSlopes_01_00.Core.Models;
using System;

namespace AutoSlopeByPointTwoSlopes_01_00.Core.Parameters
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
            int finalDrainCount,
            string version = "P.04.01")
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
                    finalDrainCount,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_RunDuration_sec",
                    runDuration_sec,
                    ref successCount, ref failCount);

                TrySetDouble(roof, "AutoSlope_LongestPath",
                    longestPath_m,
                    ref successCount, ref failCount);

                TrySetDouble(roof, "AutoSlope_SlopePercent",
                    data.SlopePercent / 100.0,
                    ref successCount, ref failCount);

                TrySetString(roof, "AutoSlope_SlopePercent_Text",
                    $"{data.SlopePercent}%",
                    ref successCount, ref failCount);

                // New: Special slope parameters
                TrySetDouble(roof, "AutoSlope_SpecialSlopePercent",
                    data.SpecialSlopePercent / 100.0,
                    ref successCount, ref failCount);

                TrySetString(roof, "AutoSlope_SpecialSlopePercent_Text",
                    $"{data.SpecialSlopePercent}%",
                    ref successCount, ref failCount);

                // New: Remaining slope parameters
                TrySetDouble(roof, "AutoSlope_RemainingSlopePercent",
                    data.RemainingSlopePercent / 100.0,
                    ref successCount, ref failCount);

                TrySetString(roof, "AutoSlope_RemainingSlopePercent_Text",
                    $"{data.RemainingSlopePercent}%",
                    ref successCount, ref failCount);

                // New: Special vertex count
                TrySetInt(roof, "AutoSlope_SpecialVertexCount",
                    data.SelectedVertexIndices?.Count ?? 0,
                    ref successCount, ref failCount);

                TrySetDouble(roof, "AutoSlope_Threshold",
                    data.ThresholdMeters * 1000.0,
                    ref successCount, ref failCount);

                TrySetString(roof, "AutoSlope_RunDate",
                    DateTime.Now.ToString("dd-MM-yy HH:mm"),
                    ref successCount, ref failCount);

                TrySetString(roof, "AutoSlope_Versions",
                    version,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_DrainToleranceMm",
                    data.EnableDrainTolerance ? (int)Math.Round(data.DrainToleranceMm) : 0,
                    ref successCount, ref failCount);

                TrySetInt(roof, "AutoSlope_DrainToleranceEnabled",
                    data.EnableDrainTolerance ? 1 : 0,
                    ref successCount, ref failCount);

                int statusValue = successCount == 0 ? 3 : failCount > 0 ? 2 : 1;
                TrySetInt(roof, "AutoSlope_Status", statusValue, ref successCount, ref failCount);

                tx.Commit();
            }

            data?.Log($"AutoSlope Parameters: {successCount} updated, {failCount} skipped");
        }

        private static void TrySetInt(Element elem, string paramName, int value, ref int ok, ref int fail)
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

        private static void TrySetDouble(Element elem, string paramName, double value, ref int ok, ref int fail)
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
}