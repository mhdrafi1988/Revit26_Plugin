// =======================================================
// File: AutoSlopeParameterWriter.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes:
//   + ridgeCount parameter added to WriteAll.
//   + Param_HighestElevation now uses TrySetDouble with
//     UnitUtils conversion (Length shared parameter fix).
//   + Param_RidgeCount written to roof element.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Infrastructure.Helpers;
using System;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Parameters
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
            int ridgeCount,
            string runDate,
            string version = "P.WithRidge.01")
        {
            if (doc == null || roof == null) return;

            int successCount = 0;
            int failCount = 0;

            using (Transaction tx = new Transaction(doc, "AutoSlope – Update Roof Parameters"))
            {
                tx.Start();

                // HighestElevation: shared param is Length type → must convert mm to feet
                TrySetDouble(roof,
                    AppConstants.Param_HighestElevation,
                    UnitUtils.ConvertToInternalUnits(highestElevation_mm, UnitTypeId.Millimeters),
                    ref successCount, ref failCount);

                TrySetInt(roof,
                    AppConstants.Param_VerticesProcessed,
                    processed,
                    ref successCount, ref failCount);

                TrySetInt(roof,
                    AppConstants.Param_VerticesSkipped,
                    skipped,
                    ref successCount, ref failCount);

                TrySetInt(roof,
                    AppConstants.Param_DrainCount,
                    finalDrainCount,
                    ref successCount, ref failCount);

                TrySetInt(roof,
                    AppConstants.Param_RunDuration,
                    runDuration_sec,
                    ref successCount, ref failCount);

                TrySetDouble(roof,
                    AppConstants.Param_LongestPath,
                    longestPath_m,
                    ref successCount, ref failCount);

                TrySetDouble(roof,
                    AppConstants.Param_SlopePercent,
                    data.SlopePercent / 100.0,
                    ref successCount, ref failCount);

                TrySetString(roof,
                    AppConstants.Param_SlopePercent_Text,
                    $"{data.SlopePercent}%",
                    ref successCount, ref failCount);

                TrySetDouble(roof,
                    AppConstants.Param_Threshold,
                    data.ThresholdMeters * 1000.0,
                    ref successCount, ref failCount);

                TrySetString(roof,
                    AppConstants.Param_RunDate,
                    runDate,
                    ref successCount, ref failCount);

                TrySetString(roof,
                    AppConstants.Param_Versions,
                    version,
                    ref successCount, ref failCount);

                TrySetInt(roof,
                    AppConstants.Param_DrainToleranceMm,
                    data.EnableDrainTolerance ? (int)Math.Round((double)data.DrainToleranceMm) : 0,
                    ref successCount, ref failCount);

                TrySetInt(roof,
                    AppConstants.Param_DrainToleranceEnabled,
                    data.EnableDrainTolerance ? 1 : 0,
                    ref successCount, ref failCount);

                // Ridge count
                TrySetInt(roof,
                    AppConstants.Param_RidgeCount,
                    ridgeCount,
                    ref successCount, ref failCount);

                int statusValue = successCount == 0
                    ? AppConstants.Status_Failed
                    : failCount > 0
                        ? AppConstants.Status_Partial
                        : AppConstants.Status_OK;

                TrySetInt(roof,
                    AppConstants.Param_Status,
                    statusValue,
                    ref successCount, ref failCount);

                tx.Commit();
            }

            data?.Log($"AutoSlope Parameters: {successCount} updated, {failCount} skipped");
        }

        // ── Private helpers with diagnostic logging ──────────────────────────

        private static void TrySetInt(
            Element elem, string paramName, int value,
            ref int ok, ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null)
                {
                    System.Diagnostics.Debug.Print($"PARAM NULL: {paramName}");
                    fail++; return;
                }
                if (p.IsReadOnly || p.StorageType != StorageType.Integer)
                {
                    System.Diagnostics.Debug.Print(
                        $"PARAM REJECTED: {paramName} IsReadOnly={p.IsReadOnly} StorageType={p.StorageType}");
                    fail++; return;
                }
                p.Set(value);
                ok++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print($"PARAM EXCEPTION: {paramName} → {ex.Message}");
                fail++;
            }
        }

        private static void TrySetDouble(
            Element elem, string paramName, double value,
            ref int ok, ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null)
                {
                    System.Diagnostics.Debug.Print($"PARAM NULL: {paramName}");
                    fail++; return;
                }
                if (p.IsReadOnly || p.StorageType != StorageType.Double)
                {
                    System.Diagnostics.Debug.Print(
                        $"PARAM REJECTED: {paramName} IsReadOnly={p.IsReadOnly} StorageType={p.StorageType}");
                    fail++; return;
                }
                p.Set(value);
                ok++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print($"PARAM EXCEPTION: {paramName} → {ex.Message}");
                fail++;
            }
        }

        private static void TrySetString(
            Element elem, string paramName, string value,
            ref int ok, ref int fail)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null)
                {
                    System.Diagnostics.Debug.Print($"PARAM NULL: {paramName}");
                    fail++; return;
                }
                if (p.IsReadOnly || p.StorageType != StorageType.String)
                {
                    System.Diagnostics.Debug.Print(
                        $"PARAM REJECTED: {paramName} IsReadOnly={p.IsReadOnly} StorageType={p.StorageType}");
                    fail++; return;
                }
                p.Set(value);
                ok++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print($"PARAM EXCEPTION: {paramName} → {ex.Message}");
                fail++;
            }
        }
    }
}
