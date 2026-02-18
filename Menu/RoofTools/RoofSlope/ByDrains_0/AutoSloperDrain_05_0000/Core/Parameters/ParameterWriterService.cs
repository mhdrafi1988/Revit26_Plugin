using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;
using Revit26_Plugin.AutoSlope.V5_00.Infrastructure.Helpers;
using System;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Parameters
{
    public class ParameterWriterService : IParameterWriter
    {
        public ParameterWriteResult WriteAll(
            Document doc,
            AutoSlopePayload payload,
            AutoSlopeMetrics metrics)
        {
            var result = new ParameterWriteResult();
            RoofBase roof = doc.GetElement(payload.RoofId) as RoofBase;

            if (roof == null) return result;

            using (Transaction tx = new Transaction(doc, "Write AutoSlope Parameters"))
            {
                tx.Start();

                // Write integer parameters
                if (TrySetInt(roof, "AutoSlope_VerticesProcessed", metrics.Processed))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                if (TrySetInt(roof, "AutoSlope_VerticesSkipped", metrics.Skipped))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                if (TrySetInt(roof, "AutoSlope_DrainCount", payload.SelectedDrains.Count))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                if (TrySetInt(roof, "AutoSlope_RunDuration_sec", metrics.DurationSeconds))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                // Write double parameters
                if (TrySetDouble(roof, "AutoSlope_HighestElevation", metrics.HighestElevation))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                if (TrySetDouble(roof, "AutoSlope_LongestPath", metrics.LongestPath))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                if (TrySetDouble(roof, "AutoSlope_SlopePercent", payload.SlopePercent))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                if (TrySetDouble(roof, "AutoSlope_Threshold", payload.ThresholdMeters))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                // Write string parameters
                if (TrySetString(roof, "AutoSlope_RunDate", metrics.RunDate))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                // Write status
                int status = result.HasFailures ? 2 : 1;
                if (TrySetInt(roof, "AutoSlope_Status", status))
                    result.SuccessCount++;
                else
                    result.FailCount++;

                tx.Commit();
            }

            payload.Log(LogColorHelper.Cyan($"📝 Parameters written: {result.SuccessCount} updated, {result.FailCount} skipped"));
            return result;
        }

        private bool TrySetInt(Element elem, string paramName, int value)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.Integer)
                    return false;

                p.Set(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TrySetDouble(Element elem, string paramName, double value)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double)
                    return false;

                p.Set(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TrySetString(Element elem, string paramName, string value)
        {
            try
            {
                Parameter p = elem.LookupParameter(paramName);
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.String)
                    return false;

                p.Set(value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}