// File: RoofData.cs
// Location: Core/Models/
// Unchanged from CSV version — namespace updated only.

using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain.V003.Core.Models
{
    public class RoofData
    {
        public RoofBase Roof { get; set; }
        public Face TopFace { get; set; }
        public List<SlabShapeVertex> Vertices { get; set; }
        public List<DrainItem> DetectedDrains { get; set; }

        public RoofData()
        {
            Vertices = new List<SlabShapeVertex>();
            DetectedDrains = new List<DrainItem>();
        }
    }
}
