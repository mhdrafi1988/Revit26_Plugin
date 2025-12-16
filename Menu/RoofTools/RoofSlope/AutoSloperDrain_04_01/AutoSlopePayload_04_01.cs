using Autodesk.Revit.DB;
using Revit22_Plugin.Asd_V4_01.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit22_Plugin.Asd_V4_01.payloads
{
    public class AutoSlopePayload_04_01
    {
        public ElementId RoofId { get; set; }
        public List<XYZ> DrainPoints { get; set; }
        //public List<XYZ> DrainPoints { get; set; }

        // Slope settings
        public double SlopePercent { get; set; }
        public double ThresholdMeters { get; set; }

        // Log callback from the WPF window
        public Action<string> Log { get; set; }

        // 🔥 ViewModel hook so handler/engine can push results back to the UI
        public RoofSlopeMainViewModel Vm { get; set; }
    }
}
