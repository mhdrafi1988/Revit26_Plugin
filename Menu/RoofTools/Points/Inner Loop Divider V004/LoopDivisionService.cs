using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DivideInnerLoops.V004.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.DivideInnerLoops.V004.Services
{
    /// <summary>
    /// Service for applying division points to boundary loops on a roof.
    /// Wraps the operation in a named transaction and notifies the user of success.
    /// </summary>
    public class LoopDivisionService
    {
        /// <summary>
        /// Applies division points to a set of selected roof loops via the slab shape editor.
        /// Only loops with <c>IsSelected = true</c> and <c>RecommendedPoints &gt; 0</c> are processed.
        /// The operation is wrapped in a single transaction named "Add Division Points".
        /// </summary>
        /// <param name="doc">The active Revit document.</param>
        /// <param name="roof">The roof whose loops will receive division points.</param>
        /// <param name="loops">Collection of <see cref="RoofLoopModel"/> to process.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="doc"/>, <paramref name="roof"/>, or <paramref name="loops"/> is null.</exception>
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
                            // Handle case when RecommendedPoints = 1
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
