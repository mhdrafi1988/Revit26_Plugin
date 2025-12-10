using Autodesk.Revit.DB;
using Revit22_Plugin.AutoSlopeV3.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit22_Plugin.AutoSlopeV3.Engine
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
