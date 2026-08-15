using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.Services
{
    /// <summary>
    /// Enables shape editing on the single picked roof and snaps raw picked
    /// points to the nearest SlabShapeVertex on that roof. Single-roof
    /// workflow — roof is picked directly (RoofDrainCalloutPlacingCommand),
    /// not collected from a view scope, so this service doesn't loop over
    /// multiple roofs or expose a "collect all roofs in view" method.
    /// </summary>
    public class RoofPointCollectionService
    {
        /// <summary>
        /// Enables SlabShapeEditor if needed (must run inside an open transaction)
        /// and returns the live editor handle. Caller owns the transaction. Public
        /// because RoofDrainCalloutPlacingCommand calls this directly right after
        /// the roof is picked, ahead of any point-picking.
        /// </summary>
        public SlabShapeEditor EnsureShapeEditingEnabled(Document doc, RoofBase roof)
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
            return editor;
        }

        /// <summary>
        /// Finds the nearest SlabShapeVertex (XY distance) on the given roof to a
        /// raw picked point, within snapToleranceFeet. Returns null if nothing is
        /// within tolerance — caller falls back to the raw pick unsnapped.
        /// Caller is responsible for the transaction.
        /// </summary>
        public CandidatePoint FindNearestVertex(Document doc, RoofBase roof, XYZ rawPick, double snapToleranceFeet)
        {
            var editor = EnsureShapeEditingEnabled(doc, roof);

            SlabShapeVertex nearest = null;
            double nearestDist = double.MaxValue;

            foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
            {
                double dx = vertex.Position.X - rawPick.X;
                double dy = vertex.Position.Y - rawPick.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = vertex;
                }
            }

            if (nearest == null || nearestDist > snapToleranceFeet)
                return null;

            return new CandidatePoint
            {
                RoofId = roof.Id,
                Position = nearest.Position,
                SnapDeltaFeet = nearestDist,
                IsSelected = true
            };
        }
    }
}
