using Autodesk.Revit.DB;
//using Revit22_Plugin.AutoSlopeV3.ViewModels;
using Revit26_Plugin.AutoSlopeByPoint_30_07.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint_30_07.Models
{
    public class AutoSlopePayload
    {
        public ElementId RoofId { get; set; }
        public List<XYZ> DrainPoints { get; set; }

        public double SlopePercent { get; set; }
        public double ThresholdMeters { get; set; }

        public Action<string> Log { get; set; }
        public AutoSlopeViewModel Vm { get; set; }
    }
}
