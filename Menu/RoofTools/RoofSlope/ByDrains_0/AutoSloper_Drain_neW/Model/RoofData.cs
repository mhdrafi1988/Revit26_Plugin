// =======================================================
// File: Models/RoofData.cs
// Description: Roof data model for processing
// =======================================================

using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Models
{
    /// <summary>
    /// Roof data extracted from Revit for slope processing
    /// </summary>
    public class RoofData
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }

        // Original vertices in feet (Revit internal units)
        public List<XYZ> OriginalVertices { get; set; }

        // Modified vertices after slope application
        public List<XYZ> ModifiedVertices { get; set; }

        // Top face reference (store as string for serialization)
        public string TopFaceReference { get; set; }

        // Face data (temporary, used during processing)
        public Face TopFace { get; set; }

        public RoofData()
        {
            OriginalVertices = new List<XYZ>();
            ModifiedVertices = new List<XYZ>();
        }

        public bool HasBeenModified => ModifiedVertices.Count > 0;
    }
}