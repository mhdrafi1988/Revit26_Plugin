using Autodesk.Revit.DB;
//using Revit22_Plugin.Asd.Models;
using System.Collections.Generic;

namespace Revit22_Plugin.Asd_V4_01.Models
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
