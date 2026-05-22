using Autodesk.Revit.DB;

namespace AutoSlopeByPointTwoSlopes_01_00.Infrastructure.Helpers
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
        public const string Param_SlopePercent_Text = "AutoSlope_SlopePercent_Text";
        public const string Param_Threshold = "AutoSlope_Threshold";
        public const string Param_RunDate = "AutoSlope_RunDate";
        public const string Param_Status = "AutoSlope_Status";
        public const string Param_Versions = "AutoSlope_Versions";
        public const string Param_DrainToleranceMm = "AutoSlope_DrainToleranceMm";
        public const string Param_DrainToleranceEnabled = "AutoSlope_DrainToleranceEnabled";

        // New parameters for multi-slope support
        public const string Param_SpecialSlopePercent = "AutoSlope_SpecialSlopePercent";
        public const string Param_RemainingSlopePercent = "AutoSlope_RemainingSlopePercent";
        public const string Param_SpecialVertexCount = "AutoSlope_SpecialVertexCount";

        public const int Status_OK = 1;
        public const int Status_Partial = 2;
        public const int Status_Failed = 3;

        public const double DefaultSlopePercent = 1.5;
        public const double DefaultSpecialSlopePercent = 2.0;
        public const double DefaultRemainingSlopePercent = 1.0;
        public const int DefaultThresholdMeters = 50;
        public const int DefaultDrainToleranceMm = 500;
        public const string DefaultExportFolder = "AutoSlope_Reports";

        public const string Color_Success = "#2ECC71";
        public const string Color_Warning = "#F1C40F";
        public const string Color_Error = "#E74C3C";
        public const string Color_Info = "#1ABC9C";
        public const string Color_Ready = "#27AE60";
        public const string Color_Processing = "#E67E22";
        public const string Color_Special = "#E74C3C";

        public const double DistanceTolerance = 0.5;
        public const double ProjectionTolerance = 0.00328084;

        // Detail circle settings
        public const double DetailCircleRadiusMm = 50.0;
        public const string DetailCircleColorRed = "#FF0000";

        // Vertex duplicate tolerance (mm)
        public const double VertexDuplicateToleranceMm = 0.5;

        // Plan View Types
        public static readonly ViewType[] PlanViewTypes = new[]
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.AreaPlan,
            ViewType.EngineeringPlan
        };
    }
}