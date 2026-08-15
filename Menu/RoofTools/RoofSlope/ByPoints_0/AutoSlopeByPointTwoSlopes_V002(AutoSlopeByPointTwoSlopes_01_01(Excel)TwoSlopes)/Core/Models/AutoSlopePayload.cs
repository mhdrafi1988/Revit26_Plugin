using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPointTwoSlopes.V002.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPointTwoSlopes.V002.Models
{
    public class AutoSlopePayload
    {
        public ElementId RoofId { get; set; }
        public List<XYZ> DrainPoints { get; set; }
        public double SlopePercent { get; set; }
        public double ThresholdMeters { get; set; }
        public Action<string> Log { get; set; }
        public AutoSlopeViewModel Vm { get; set; }
        public ExportConfig ExportConfig { get; set; }

        // Drain tolerance
        public bool EnableDrainTolerance { get; set; }
        public double DrainToleranceMm { get; set; }

        // Multi-slope support
        public double SpecialSlopePercent { get; set; }
        public double RemainingSlopePercent { get; set; }
        public HashSet<int> SelectedVertexIndices { get; set; }

        // Track which vertices were processed with which slope
        public Dictionary<int, double> VertexSlopeMapping { get; set; }
    }
}