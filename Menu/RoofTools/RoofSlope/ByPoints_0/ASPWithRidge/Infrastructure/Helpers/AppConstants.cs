// =======================================================
// File: AppConstants.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// Changes:
//   + Param_RidgeCount              — shared parameter name.
//   + DefaultRidgeDetectionEnabled  — feature off by default.
//   + DefaultDrainGroupRadiusMm     — 500 mm grouping radius.
//   + DefaultRidgeLineToleranceMm   — 500 mm line membership band.
//   DefaultRidgeRatioTolerance removed — ratio check replaced
//   by drain-group geometry; no ratio constant needed.
// =======================================================

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Infrastructure.Helpers
{
    public static class AppConstants
    {
        // ── Shared parameter names ───────────────────────────────────────────
        public const string Param_HighestElevation       = "AutoSlope_HighestElevation";
        public const string Param_VerticesProcessed      = "AutoSlope_VerticesProcessed";
        public const string Param_VerticesSkipped        = "AutoSlope_VerticesSkipped";
        public const string Param_DrainCount             = "AutoSlope_DrainCount";
        public const string Param_RunDuration            = "AutoSlope_RunDuration_sec";
        public const string Param_LongestPath            = "AutoSlope_LongestPath";
        public const string Param_SlopePercent           = "AutoSlope_SlopePercent";
        public const string Param_SlopePercent_Text      = "AutoSlope_SlopePercent_Text";
        public const string Param_Threshold              = "AutoSlope_Threshold";
        public const string Param_RunDate                = "AutoSlope_RunDate";
        public const string Param_Status                 = "AutoSlope_Status";
        public const string Param_Versions               = "AutoSlope_Versions";
        public const string Param_DrainToleranceMm       = "AutoSlope_DrainToleranceMm";
        public const string Param_DrainToleranceEnabled  = "AutoSlope_DrainToleranceEnabled";
        public const string Param_RidgeCount             = "AutoSlope_RidgeCount";

        // ── Status codes ─────────────────────────────────────────────────────
        public const int Status_OK      = 1;
        public const int Status_Partial = 2;
        public const int Status_Failed  = 3;

        // ── Defaults ─────────────────────────────────────────────────────────
        public const double DefaultSlopePercent         = 1.5;
        public const int    DefaultThresholdMeters      = 50;
        public const int    DefaultDrainToleranceMm     = 500;
        public const string DefaultExportFolder         = "AutoSlope_Reports";

        // Ridge detection defaults
        public const bool DefaultRidgeDetectionEnabled  = true;  // on until user DISABLE
        public const int  DefaultDrainGroupRadiusMm     = 500;    // drains within 500 mm = same group
        public const int  DefaultRidgeLineToleranceMm   = 500;    // vertices within 500 mm of ridge line

        // ── UI colors ────────────────────────────────────────────────────────
        public const string Color_Success    = "#2ECC71";
        public const string Color_Warning    = "#F1C40F";
        public const string Color_Error      = "#E74C3C";
        public const string Color_Info       = "#1ABC9C";
        public const string Color_Ready      = "#27AE60";
        public const string Color_Processing = "#E67E22";
        public const string Color_Ridge      = "#9B59B6";   // purple — ridge highlight in log

        // ── Tolerances ───────────────────────────────────────────────────────
        public const double DistanceTolerance   = 0.5;
        public const double ProjectionTolerance = 0.00328084;
    }
}
