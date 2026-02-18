using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Models
{
    public class RoofData
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public List<VertexData> OriginalVertices { get; set; }
        public List<DrainItem> Drains { get; set; }
        public DateTime LastProcessed { get; set; }
        public double LastSlopePercent { get; set; }
    }
}