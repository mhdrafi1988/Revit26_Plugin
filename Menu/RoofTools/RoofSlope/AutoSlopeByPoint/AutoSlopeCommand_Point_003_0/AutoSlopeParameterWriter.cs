using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.Helpers;
using System;

namespace Revit26_Plugin.AutoSlopeByPoint.Engine
{
    public static class AutoSlopeParameterWriter
    {
        public static void UpdateAllParameters(
            Document doc,
            RoofBase roof,
            AutoSlopePayload data,
            double highestElevationMeters,
            double longestPathMeters,
            int processed,
            int skipped,
            double durationSeconds,
            Action<string> log)
        {
            try
            {
                if (doc == null)
                {
                    log(LogColorHelper.Red("❌ Parameter Update Failed: Document is null."));
                    return;
                }

                if (roof == null)
                {
                    log(LogColorHelper.Red("❌ Parameter Update Failed: Roof element is null."));
                    return;
                }

                // -----------------------------------------
                // LOOKUP ALL PARAMETERS
                // -----------------------------------------
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

                // Check existence
                if (pSlope == null || pHigh == null || pThreshold == null ||
                    pLongest == null || pProcessed == null || pSkipped == null ||
                    pDrainCount == null || pRunDate == null || pDuration == null || pStatus == null)
                {
                    log(LogColorHelper.Red("⚠️ One or more shared parameters are missing! No values were written."));
                    return;
                }

                // Read-only check
                if (pSlope.IsReadOnly || pHigh.IsReadOnly || pThreshold.IsReadOnly ||
                    pLongest.IsReadOnly || pProcessed.IsReadOnly || pSkipped.IsReadOnly ||
                    pDrainCount.IsReadOnly || pRunDate.IsReadOnly || pDuration.IsReadOnly || pStatus.IsReadOnly)
                {
                    log(LogColorHelper.Red("⚠️ One or more parameters are READ-ONLY. Cannot update."));
                    return;
                }

                // -----------------------------------------
                // Write values inside ONE transaction
                // -----------------------------------------
                using (Transaction tx = new Transaction(doc, "Update AutoSlope Parameters"))
                {
                    tx.Start();

                    pSlope.Set(data.SlopePercent);                // Number
                    pHigh.Set(highestElevationMeters);            // Distance (meters)
                    pThreshold.Set(data.ThresholdMeters);         // Length (meters)
                    pLongest.Set(longestPathMeters);              // Length (meters)
                    pProcessed.Set(processed);                    // Integer
                    pSkipped.Set(skipped);                        // Integer
                    pDrainCount.Set(data.DrainPoints.Count);      // Integer
                    pDuration.Set(durationSeconds);               // Time interval (seconds)

                    // Run date as seconds since midnight (Revit stores TimeInterval)
                    double nowStamp = (DateTime.Now - DateTime.Today).TotalSeconds;
                    pRunDate.Set(nowStamp);

                    // Status = 1 (true)
                    pStatus.Set(1);

                    tx.Commit();
                }

                log(LogColorHelper.Green("✔ All AutoSlope shared parameters updated successfully!"));
            }
            catch (Exception ex)
            {
                log(LogColorHelper.Red("❌ Error updating shared parameters: " + ex.Message));
            }
        }
    }
}
