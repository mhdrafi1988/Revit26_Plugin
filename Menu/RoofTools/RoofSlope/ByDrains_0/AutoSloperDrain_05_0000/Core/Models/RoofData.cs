using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Models
{
    public class RoofData
    {
        public RoofBase Roof { get; set; }
        public Face TopFace { get; set; }
        public List<SlabShapeVertex> Vertices { get; set; } = new();
        public List<DrainItem> DetectedDrains { get; set; } = new();
    }
}