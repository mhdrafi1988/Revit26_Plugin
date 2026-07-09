using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.Models;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V001.Services
{
    /// <summary>
    /// Collects RoofBase elements visible in a view and extracts zero-offset
    /// SlabShapeVertex points from each, enabling shape editing where needed.
    /// </summary>
    public class RoofPointCollectionService
    {
        private const double OffsetEpsilonFeet = 1e-6;

        public List<RoofBase> CollectRoofsInView(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(RoofBase))
                .WhereElementIsNotElementType()
                .Cast<RoofBase>()
                .ToList();
        }

        /// <summary>
        /// For a single roof: enables SlabShapeEditor if needed (must run inside an
        /// open transaction), then returns all points whose VertexOffset is ~0.
        /// Caller is responsible for the transaction and for logging.
        /// </summary>
        public List<XYZ> GetZeroOffsetPoints(Document doc, RoofBase roof, IList<LogEntry> log)
        {
            var editor = roof.GetSlabShapeEditor();

            if (!editor.IsEnabled)
            {
                editor.Enable();
                // doc.Regenerate() invalidates any previously-obtained editor handle —
                // always re-fetch after a regenerate.
                doc.Regenerate();
                editor = roof.GetSlabShapeEditor();
            }

            var zeroPoints = new List<XYZ>();
            foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
            {
                // Use Position.Z as the offset from the base plane (commonly used for roof shape vertices)
                if (Math.Abs(vertex.Position.Z) <= OffsetEpsilonFeet)
                    zeroPoints.Add(vertex.Position);
            }

            log.Add(new LogEntry(LogLevel.Info,
                $"Roof {roof.Id}: {editor.SlabShapeVertices.Size} shape points, {zeroPoints.Count} at zero offset"));

            return zeroPoints;
        }
    }
}
