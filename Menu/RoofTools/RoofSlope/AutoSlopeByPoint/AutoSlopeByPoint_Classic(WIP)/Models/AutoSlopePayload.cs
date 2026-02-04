using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Models
{
    public class AutoSlopePayload
    {
        public ElementId RoofId { get; set; }
        public List<XYZ> DrainPoints { get; set; }
        public double SlopePercent { get; set; }
        public int ThresholdMeters { get; set; }
        public string ExportFolderPath { get; set; }
        public Action<string> LogCallback { get; set; }
        public AutoSlopeViewModel ViewModel { get; set; }
    }
}