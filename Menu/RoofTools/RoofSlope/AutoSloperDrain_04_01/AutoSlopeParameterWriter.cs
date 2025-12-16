using Autodesk.Revit.DB;
using Revit22_Plugin.Asd_V4_01.payloads;   //  <-- added
using System;

namespace Revit22_Plugin.Asd_V4_01.Services
{
    public static class AutoSlopeParameterWriter
    {
        public static void UpdateAllParameters(
            Document doc,
            RoofBase roof,
            AutoSlopePayload_04_01 data,      // <--- changed from AutoSlopePayload
            double highestElevationMeters,
            double longestPathMeters,
            int processed,
            int skipped,
            double durationSeconds,
            Action<string> log)
        {
            try
            {
                log?.Invoke("⏳ Starting shared parameter update...");

                if (doc == null)
                {
                    log?.Invoke("❌ Document is null.");
                    return;
                }

                if (roof == null)
                {
                    log?.Invoke("❌ Roof is null.");
                    return;
                }

                Parameter pSlope = roof.LookupParameter("AutoSlope_SlopePercent");
                Parameter pHigh = roof.LookupParameter("AutoSlope_HighestElevation_m");
                Parameter pThreshold = roof.LookupParameter("AutoSlope_Threshold_m");
                Parameter pLongest = roof.LookupParameter("AutoSlope_LongestPath_m");
                Parameter pProcessed = roof.LookupParameter("AutoSlope_VerticesProcessed");
                Parameter pSkipped = roof.LookupParameter("AutoSlope_VerticesSkipped");
                Parameter pDrainCount = roof.LookupParameter("AutoSlope_DrainCount");
                Parameter pRunDate = roof.LookupParameter("AutoSlope_RunDate");
                Parameter pDuration = roof.LookupParameter("AutoSlope_RunDuration_sec");
                Parameter pStatus = roof.LookupParameter("AutoSlope_Status");

                using (Transaction tx = new Transaction(doc, "Update AutoSlope Parameters"))
                {
                    tx.Start();

                    // Safe setter function
                    void TrySet(Parameter p, object value, string name)
                    {
                        if (p == null)
                        {
                            log?.Invoke($"⚠ Missing parameter: {name}");
                            return;
                        }

                        try
                        {
                            if (value is string s) p.Set(s);
                            else if (value is int i) p.Set(i);
                            else if (value is double d) p.Set(d);
                            else log?.Invoke($"⚠ Unsupported type for {name}");

                            log?.Invoke($"✔ Updated: {name}");
                        }
                        catch (Exception ex)
                        {
                            log?.Invoke($"❌ Failed to set {name}: {ex.Message}");
                        }
                    }

                    // Numeric values
                    TrySet(pSlope, data.SlopePercent, "AutoSlope_SlopePercent");
                    TrySet(pHigh, highestElevationMeters, "AutoSlope_HighestElevation_m");
                    TrySet(pThreshold, data.ThresholdMeters, "AutoSlope_Threshold_m");
                    TrySet(pLongest, longestPathMeters, "AutoSlope_LongestPath_m");
                    TrySet(pProcessed, processed, "AutoSlope_VerticesProcessed");
                    TrySet(pSkipped, skipped, "AutoSlope_VerticesSkipped");
                    TrySet(pDrainCount, data.DrainPoints.Count, "AutoSlope_DrainCount");
                    TrySet(pDuration, durationSeconds, "AutoSlope_RunDuration_sec");

                    // TEXT
                    TrySet(pRunDate, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "AutoSlope_RunDate");

                    // YES/NO
                    TrySet(pStatus, 1, "AutoSlope_Status");

                    tx.Commit();
                }

                log?.Invoke("✔ Shared parameters updated successfully.");
            }
            catch (Exception ex)
            {
                log?.Invoke("❌ Writer crash: " + ex.Message);
            }
        }
    }
}
