namespace Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers
{
    public static class AppConstants
    {
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

        public const int Status_OK = 1;
        public const int Status_Partial = 2;
        public const int Status_Failed = 3;

        public const double DefaultSlopePercent = 1.5;
        public const int DefaultThresholdMeters = 50;
        public const string DefaultExportFolder = "AutoSlope_Reports";

        public const string Color_Success = "#2ECC71";
        public const string Color_Warning = "#F1C40F";
        public const string Color_Error = "#E74C3C";
        public const string Color_Info = "#1ABC9C";
        public const string Color_Ready = "#27AE60";
        public const string Color_Processing = "#E67E22";

        public const double DistanceTolerance = 0.5;
        public const double ProjectionTolerance = 0.00328084;
    }
}