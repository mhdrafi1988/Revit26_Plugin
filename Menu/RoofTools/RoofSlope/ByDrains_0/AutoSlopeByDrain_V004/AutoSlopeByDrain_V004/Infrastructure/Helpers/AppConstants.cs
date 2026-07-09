// File: AppConstants.cs
// Location: Revit26_Plugin.AutoSlopeByDrain.V004.Infrastructure.Helpers

namespace Revit26_Plugin.AutoSlopeByDrain.V004.Infrastructure.Helpers
{
    public static class AppConstants
    {
        // Parameter Names - Match Part 01 exactly
        public const string Param_HighestElevation = "AutoSlope_HighestElevation";
        public const string Param_VerticesProcessed = "AutoSlope_VerticesProcessed";
        public const string Param_VerticesSkipped = "AutoSlope_VerticesSkipped";
        public const string Param_DrainCount = "AutoSlope_DrainCount";
        public const string Param_RunDuration = "AutoSlope_RunDuration_sec";
        public const string Param_LongestPath = "AutoSlope_LongestPath";
        public const string Param_SlopePercent = "AutoSlope_SlopePercent";
        public const string Param_Threshold = "AutoSlope_Threshold";
        public const string Param_RunDate = "AutoSlope_RunDate";
        public const string Param_Status = "AutoSlope_Status";
        public const string Param_Versions = "AutoSlope_Versions";

        // Status Values
        public const int Status_OK = 1;
        public const int Status_Partial = 2;
        public const int Status_Failed = 3;

        // Defaults (added in V003 to match AutoSlopeByPoint's AppConstants)
        public const double DefaultSlopePercent = 1.5;
        public const int DefaultThresholdMeters = 30;
        public const int DefaultPathSampleCount = 50;
        public const string DefaultExportFolder = "AutoSlope_Reports";
    }
}