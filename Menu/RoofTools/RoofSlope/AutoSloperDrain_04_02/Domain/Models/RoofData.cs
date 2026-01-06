using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit22_Plugin.V4_02.Domain.Models
{
    public class RoofData
    {
        // ===============================
        // REVIT REFERENCES
        // ===============================
        public RoofBase Roof { get; set; }
        public Face TopFace { get; set; }

        // ===============================
        // SHAPE EDITING DATA
        // ===============================
        public List<SlabShapeVertex> Vertices { get; set; }

        // ===============================
        // DETECTED DRAINS
        // ===============================
        public List<DrainItem> DetectedDrains { get; set; }

        // ===============================
        // CTOR
        // ===============================
        public RoofData()
        {
            Vertices = new List<SlabShapeVertex>();
            DetectedDrains = new List<DrainItem>();
        }
    }
}
