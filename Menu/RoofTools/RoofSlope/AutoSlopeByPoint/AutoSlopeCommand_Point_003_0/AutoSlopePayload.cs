using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.Engine
{
    public class AutoSlopePayload
    {
        public ElementId RoofId { get; set; }
        public List<XYZ> DrainPoints { get; set; }

        // Slope settings
        public double SlopePercent { get; set; }
        public double ThresholdMeters { get; set; }

        // Log callback from the WPF window
        public Action<string> Log { get; set; }

        // 🔥 ViewModel hook so handler/engine can push results back to the UI
        public AutoSlopeViewModel Vm { get; set; }
    }
}
