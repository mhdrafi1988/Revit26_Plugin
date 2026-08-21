// File: AppConstants.cs
// Location: Infrastructure/Helpers/
// Merged from AutoSlopeByPoint V018 and AutoSlopeByDrain V004 — parameter
// names were already identical between both tools (both target the same
// roof instance parameters), so this is a straight union of constants used
// by either side, plus new V005 defaults for the ported/added fields.

namespace Revit26_Plugin.AutoSlopeByDrain.V006.Infrastructure.Helpers
{
    public static class AppConstants
    {
        // ── Roof instance parameter names ───────────────────────────────────
        public const string Param_HighestElevation      = "AutoSlope_HighestElevation";
        public const string Param_VerticesProcessed     = "AutoSlope_VerticesProcessed";
        public const string Param_VerticesSkipped       = "AutoSlope_VerticesSkipped";
        public const string Param_DrainCount            = "AutoSlope_DrainCount";
        public const string Param_RunDuration           = "AutoSlope_RunDuration_sec";
        public const string Param_LongestPath           = "AutoSlope_LongestPath";
        public const string Param_SlopePercent          = "AutoSlope_SlopePercent";
        public const string Param_Threshold             = "AutoSlope_Threshold";
        public const string Param_RunDate               = "AutoSlope_RunDate";
        public const string Param_Status                = "AutoSlope_Status";
        public const string Param_Versions              = "AutoSlope_Versions";

        // Status Values
        public const int Status_OK      = 1;
        public const int Status_Partial = 2;
        public const int Status_Failed  = 3;

        // ── Defaults ─────────────────────────────────────────────────────────
        public const double DefaultSlopePercent          = 1.5;

        /// <summary>Default for Max Edge Distance (graph connectivity / ConnectionThresholdMeters).</summary>
        public const int DefaultConnectionThresholdMeters = 30;

        /// <summary>NEW (V005): default for Max Path Distance (result cutoff / ThresholdMeters), ported from ByPoint.</summary>
        public const int DefaultThresholdMeters          = 40;

        public const int DefaultPathSampleCount          = 10;
        public const string DefaultExportFolder          = "AutoSlope_Reports";

        public const double DistanceTolerance   = 0.5;
        public const double ProjectionTolerance = 0.00328084;

        // ── Status colors (hex, for UI badges) ──────────────────────────────
        public const string Color_Success    = "#2ECC71";
        public const string Color_Warning    = "#F1C40F";
        public const string Color_Error      = "#E74C3C";
        public const string Color_Info       = "#1ABC9C";
        public const string Color_Ready      = "#27AE60";
        public const string Color_Processing = "#E67E22";
    }
}
