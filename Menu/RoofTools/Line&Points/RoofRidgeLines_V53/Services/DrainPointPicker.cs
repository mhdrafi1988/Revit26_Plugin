using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V53.Services
{
    /// <summary>
    /// Utility to pick drain points (shape editing vertices) on a roof.
    /// </summary>
    public static class DrainPointPicker
    {
        /// <summary>
        /// Lets the user pick points on the roof surface and returns the nearest shape editing vertices.
        /// </summary>
        /// <param name="uiDoc">Active UIDocument</param>
        /// <param name="roof">The roof on which drains are placed</param>
        /// <returns>List of XYZ positions (Z=0) of the selected drain vertices.</returns>
        /// <exception cref="InvalidOperationException">If shape editing cannot be enabled or no vertices exist.</exception>
        /// <exception cref="Autodesk.Revit.Exceptions.OperationCanceledException">If user cancels picking.</exception>
        public static List<XYZ> PickDrainPoints(UIDocument uiDoc, RoofBase roof)
        {
            Document doc = uiDoc.Document;
            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null)
                throw new InvalidOperationException("This roof does not support shape editing (GetSlabShapeEditor() returned null).");

            List<XYZ> result = null;

            using (TransactionGroup tg = new TransactionGroup(doc, "Temporary Shape Editing for Drain Selection"))
            {
                tg.Start();

                // Enable shape editing inside a transaction
                using (Transaction tx = new Transaction(doc, "Enable Roof Shape Editing"))
                {
                    tx.Start();
                    if (!editor.IsEnabled)
                        editor.Enable();
                    tx.Commit();
                }

                // Let user pick points on the roof
                IList<Reference> pickedRefs;
                try
                {
                    pickedRefs = uiDoc.Selection.PickObjects(
                        ObjectType.PointOnElement,
                        "Pick drain points on the roof surface (shape editing vertices). Press ESC to cancel.");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    tg.RollBack();
                    throw; // rethrow so the command can handle cancellation
                }

                // Validate that all picked points belong to the selected roof
                var drainPoints = new List<XYZ>();
                foreach (var refPick in pickedRefs)
                {
                    if (refPick.ElementId != roof.Id)
                    {
                        tg.RollBack();
                        throw new InvalidOperationException("One or more points were not picked on the selected roof.");
                    }
                    drainPoints.Add(refPick.GlobalPoint);
                }

                if (drainPoints.Count == 0)
                {
                    tg.RollBack();
                    throw new InvalidOperationException("No drain points were picked.");
                }

                // Retrieve all existing shape vertices from the roof
                var vertices = new List<XYZ>();
                foreach (var vertexObj in editor.SlabShapeVertices)
                {
                    var type = vertexObj.GetType();
                    var prop = type.GetProperty("Position");
                    if (prop != null && prop.GetValue(vertexObj) is XYZ pos)
                        vertices.Add(pos);
                }

                if (vertices.Count == 0)
                {
                    tg.RollBack();
                    throw new InvalidOperationException("The roof has no shape editing vertices. Cannot pick drains.");
                }

                // Snap each clicked point to the nearest existing vertex
                var finalDrainLocations = new List<XYZ>();
                foreach (var clickedPt in drainPoints)
                {
                    XYZ nearest = FindNearestVertex(clickedPt, vertices);
                    finalDrainLocations.Add(new XYZ(nearest.X, nearest.Y, 0));
                }

                result = finalDrainLocations;

                // Roll back the transaction group – this disables shape editing
                tg.RollBack();
            }

            return result;
        }

        private static XYZ FindNearestVertex(XYZ clickedPoint, List<XYZ> vertices)
        {
            double minDistSq = double.MaxValue;
            XYZ nearest = null;
            foreach (var v in vertices)
            {
                double dx = clickedPoint.X - v.X;
                double dy = clickedPoint.Y - v.Y;
                double distSq = dx * dx + dy * dy;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = v;
                }
            }
            return nearest ?? XYZ.Zero;
        }
    }
}