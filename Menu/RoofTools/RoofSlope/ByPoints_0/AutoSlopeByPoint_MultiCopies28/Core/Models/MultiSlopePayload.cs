// =======================================================
// File: MultiSlopePayload.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.MultiCopies28
// Purpose: Input for MultiSlopeVariantEngine.Execute — the source
//          roof/drain selection plus the 4 slope rows and every
//          setting carried over from AutoSlopePayload (threshold,
//          drain tolerance, curve-intersection, export, circle
//          markers), applied identically to every generated copy.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Core.Models
{
    public class MultiSlopePayload
    {
        // ── Source roof / drains ───────────────────────────────
        public ElementId RoofId { get; set; }
        public List<XYZ> PickedDrainPoints { get; set; }
        public List<XYZ> DrainPoints { get; set; }

        // ── Slope rows ──────────────────────────────────────────
        public List<SlopeRowSetting> SlopeRows { get; set; }

        // ── Shared settings, applied identically to every copy ──
        public double ThresholdMeters { get; set; }
        public bool EnableDrainTolerance { get; set; }
        public int DrainToleranceMm { get; set; }
        public bool InsertCurveIntersectionPoints { get; set; }
        public ExportConfig ExportConfig { get; set; }
        public string ProjectTitle { get; set; }

        public CircleMarkerGroup DrainMarkerGroup { get; set; }
        public CircleMarkerGroup HighestPointMarkerGroup { get; set; }
        public CircleMarkerGroup AllowedOffsetMarkerGroup { get; set; }
        public double AllowedOffsetThresholdMm { get; set; }

        // ── Workset naming ───────────────────────────────────────
        /// <summary>Format string with one placeholder for the slope value, e.g. "Roof_Slope_{0}%".</summary>
        public string WorksetNameFormat { get; set; } = "Roof_Slope_{0}%";

        // ── Callbacks ─────────────────────────────────────────────
        public Action<LogEntry> Log { get; set; }

        /// <summary>Called exactly once when every active slope row has been attempted.</summary>
        public Action<MultiSlopeRunSummary> OnCompleted { get; set; }
    }
}
