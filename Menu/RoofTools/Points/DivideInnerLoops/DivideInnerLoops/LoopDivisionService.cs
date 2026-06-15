using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.PDCV1.Models;
using System;  // Added this missing namespace for ArgumentNullException
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.PDCV1.Services
{
    public class LoopDivisionService
    {
        public void AddDivisionPoints(Document doc, RoofBase roof, IEnumerable<RoofLoopModel> loops)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (roof == null) throw new ArgumentNullException(nameof(roof));
            if (loops == null) throw new ArgumentNullException(nameof(loops));

            var validLoops = loops.Where(l => l != null && l.IsSelected && l.RecommendedPoints > 0).ToList();
            if (!validLoops.Any()) return;

            using (Transaction tx = new Transaction(doc, "Add Division Points"))
            {
                tx.Start();

                var editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled)
                    editor.Enable();

                int pointsAdded = 0;

                foreach (var loop in validLoops)
                {
                    if (loop.Geometry == null) continue;

                    foreach (Curve curve in loop.Geometry)
                    {
                        // Add points along the curve
                        for (int i = 0; i < loop.RecommendedPoints; i++)
                        {
                            // Fix: Handle case when RecommendedPoints = 1
                            double param = loop.RecommendedPoints == 1 ? 0.5 : (double)i / (loop.RecommendedPoints - 1);
                            XYZ point = curve.Evaluate(param, true);

                            try
                            {
                                editor.AddPoint(point);
                                pointsAdded++;
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException)
                            {
                                // Point might already exist - skip
                                continue;
                            }
                        }
                    }
                }

                tx.Commit();

                TaskDialog.Show("Success", $"Added {pointsAdded} division points to {validLoops.Count} loops.");
            }
        }
    }
}