// File: AutoSlopeParameterWriter.cs
// Location: Revit26_Plugin.Asd_19.Core.Parameters

using Autodesk.Revit.DB;
using Revit26_Plugin.Asd_19.Infrastructure.Helpers;
using Revit26_Plugin.Asd_19.Models;
using System;

namespace Revit26_Plugin.Asd_19.Core.Parameters
{
    public class AutoSlopeParameterWriter : IParameterWriter
    {
        public ParameterWriteResult WriteAll(
            Document doc,
            RoofBase roof,
            DrainExportMetrics metrics,
            double slopePercent,
            double thresholdMeters,
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

                // Double parameters (stored in Revit internal units - feet)
                TrySetDouble(roof, AppConstants.Param_LongestPath,
                    metrics.LongestPathM, // Store in meters (Revit can display with unit)
                    ref successCount, ref failCount);

                TrySetDouble(roof, AppConstants.Param_SlopePercent,
                    slopePercent,
                    ref successCount, ref failCount);

                TrySetDouble(roof, AppConstants.Param_Threshold,
                    thresholdMeters, // Store in meters
                    ref successCount, ref failCount);

                // String parameters
                TrySetString(roof, AppConstants.Param_RunDate,
                    DateTime.Now.ToString("dd-MM-yy HH:mm"),
                    ref successCount, ref failCount);

                // Status parameter - calculate based on success/failure
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
                if (p == null)
                {
                    fail++;
                    return;
                }

                if (p.IsReadOnly)
                {
                    fail++;
                    return;
                }

                if (p.StorageType != StorageType.Integer)
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
                if (p == null)
                {
                    fail++;
                    return;
                }

                if (p.IsReadOnly)
                {
                    fail++;
                    return;
                }

                if (p.StorageType != StorageType.Double)
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
                if (p == null)
                {
                    fail++;
                    return;
                }

                if (p.IsReadOnly)
                {
                    fail++;
                    return;
                }

                if (p.StorageType != StorageType.String)
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