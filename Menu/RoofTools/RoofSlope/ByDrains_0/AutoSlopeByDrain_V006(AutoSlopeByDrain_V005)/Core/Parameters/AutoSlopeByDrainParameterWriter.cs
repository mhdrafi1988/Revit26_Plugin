// File: AutoSlopeDrainParameterWriter.cs
// Location: Core/Parameters/
// Base: ported from AutoSlopeByDrain V004.
//
// BUG FIXES (flagged during audit, not silently changed):
//   1. Param_Threshold unit inconsistency — ByPoint's writer stored
//      data.ThresholdMeters * 1000.0 (i.e. millimeters) into this parameter;
//      V004's writer stored the raw meters value into the SAME parameter
//      name. Since both tools share this parameter on the same roof
//      instance, this was a real inconsistency depending on which tool last
//      wrote it. FIXED: stores meters consistently (matches V004's own
//      in-code comment "// Store in meters"), and the docstring makes the
//      unit explicit.
//   2. Version string was hardcoded "V004" inline instead of passed as a
//      parameter — meant every future version bump required editing this
//      file directly. FIXED: version is now a required parameter, sourced
//      from AutoSlopeDrainResult.Version ("006" default, confirmed by Rafi).

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain.V006.Infrastructure.Helpers;
using Revit26_Plugin.AutoSlopeByDrain.V006.Core.Models;
using System;

namespace Revit26_Plugin.AutoSlopeByDrain.V006.Core.Parameters
{
    public class AutoSlopeDrainParameterWriter : IParameterWriter
    {
        public ParameterWriteResult WriteAll(
            Document doc,
            RoofBase roof,
            DrainExportMetrics metrics,
            double slopePercent,
            double thresholdMeters,
            Action<string> logAction = null)
        {
            return WriteAll(doc, roof, metrics, slopePercent, thresholdMeters, metrics?.Version ?? "006", logAction);
        }

        /// <summary>
        /// Overload accepting an explicit version string. The interface-required
        /// overload above falls back to metrics.Version so existing call sites
        /// that only know IParameterWriter still work.
        /// </summary>
        public ParameterWriteResult WriteAll(
            Document doc,
            RoofBase roof,
            DrainExportMetrics metrics,
            double slopePercent,
            double thresholdMeters,
            string version,
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
                TrySetInt(roof, AppConstants.Param_HighestElevation,
                    (int)Math.Round(metrics.HighestElevationMm),
                    ref successCount, ref failCount);

                TrySetInt(roof, AppConstants.Param_VerticesProcessed,
                    metrics.ProcessedVertices,
                    ref successCount, ref failCount);

                TrySetInt(roof, AppConstants.Param_VerticesSkipped,
                    metrics.SkippedVertices,
                    ref successCount, ref failCount);

                TrySetInt(roof, AppConstants.Param_DrainCount,
                    metrics.DrainCount,
                    ref successCount, ref failCount);

                TrySetInt(roof, AppConstants.Param_RunDuration,
                    metrics.RunDurationSec,
                    ref successCount, ref failCount);

                // Double parameters
                TrySetDouble(roof, AppConstants.Param_LongestPath,
                    metrics.LongestPathM, // stored in meters
                    ref successCount, ref failCount);

                TrySetDouble(roof, AppConstants.Param_SlopePercent,
                    slopePercent,
                    ref successCount, ref failCount);

                // FIX: stored in meters consistently (was inconsistent between
                // ByPoint (mm) and ByDrain (m) writers targeting the same parameter name).
                TrySetDouble(roof, AppConstants.Param_Threshold,
                    thresholdMeters,
                    ref successCount, ref failCount);

                // String parameters
                TrySetString(roof, AppConstants.Param_RunDate,
                    metrics.RunDate ?? DateTime.Now.ToString("dd-MM-yy HH:mm"),
                    ref successCount, ref failCount);

                // FIX: version now passed in rather than hardcoded.
                TrySetString(roof, AppConstants.Param_Versions,
                    version ?? "006",
                    ref successCount, ref failCount);

                int statusValue = CalculateStatusValue(successCount, failCount);
                TrySetInt(roof, AppConstants.Param_Status, statusValue, ref successCount, ref failCount);

                tx.Commit();
            }

            string logMessage = $"AutoSlope Parameters: {successCount} updated, {failCount} skipped";
            logAction?.Invoke(logMessage);

            return new ParameterWriteResult
            {
                SuccessCount = successCount,
                FailCount = failCount
            };
        }

        private int CalculateStatusValue(int successCount, int failCount)
        {
            if (successCount == 0) return AppConstants.Status_Failed;
            if (failCount > 0) return AppConstants.Status_Partial;
            return AppConstants.Status_OK;
        }

        private void TrySetInt(Element elem, string paramName, int value,
            ref int success, ref int fail)
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
                success++;
            }
            catch
            {
                fail++;
            }
        }

        private void TrySetDouble(Element elem, string paramName, double value,
            ref int success, ref int fail)
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
                success++;
            }
            catch
            {
                fail++;
            }
        }

        private void TrySetString(Element elem, string paramName, string value,
            ref int success, ref int fail)
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
                success++;
            }
            catch
            {
                fail++;
            }
        }
    }
}
