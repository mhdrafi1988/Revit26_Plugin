using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;
using Revit26_Plugin.AutoSlope.V5_00.UI.ViewModels;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Models
{
    public class AutoSlopePayload
    {
        public ElementId RoofId { get; set; }
        public RoofData RoofData { get; set; }
        public List<DrainItem> SelectedDrains { get; set; }
        public double SlopePercent { get; set; }
        public double ThresholdMeters { get; set; }
        public Action<string> Log { get; set; }
        public MainViewModel Vm { get; set; }
        public ExportConfig ExportConfig { get; set; }
    }
}