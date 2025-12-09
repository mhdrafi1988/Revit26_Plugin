using Autodesk.Revit.DB;
using System.Collections.Generic;
using Revit22_Plugin.Asd.Models;
using Revit22_Plugin.Asd.Services;  

namespace Revit22_Plugin.Asd.Models
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