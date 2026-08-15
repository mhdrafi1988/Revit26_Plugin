using Autodesk.Revit.DB;
using Revit26_Plugin.InnerLoopDivider.V007.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.InnerLoopDivider.V007.Services
{
    /// <summary>
    /// Applies division points to boundary loops on a roof via the slab shape editor.
    /// No TaskDialogs are raised — results are returned to the caller for log display.
    /// </summary>
    public class LoopDivisionService
    {
        /// <summary>
        /// Applies division points to selected loops.
        /// </summary>
        /// <returns>Number of points successfully added, or -1 on transaction failure.</returns>
        public int AddDivisionPoints(Document doc, RoofBase roof, IEnumerable<RoofLoopModel> loops)
        {
            if (doc  == null) throw new ArgumentNullException(nameof(doc));
            if (roof == null) throw new ArgumentNullException(nameof(roof));
            if (loops == null) throw new ArgumentNullException(nameof(loops));

            var validLoops = loops
                .Where(l => l != null && l.IsSelected && l.RecommendedPoints > 0)
                .ToList();

            if (!validLoops.Any()) return 0;

            try
            {
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
                            for (int i = 0; i < loop.RecommendedPoints; i++)
                            {
                                double param = loop.RecommendedPoints == 1
                                    ? 0.5
                                    : (double)i / (loop.RecommendedPoints - 1);

                                XYZ point = curve.Evaluate(param, true);

                                try
                                {
                                    editor.AddPoint(point);
                                    pointsAdded++;
                                }
                                catch (Autodesk.Revit.Exceptions.ArgumentException)
                                {
                                    // Duplicate point — skip silently
                                }
                            }
                        }
                    }

                    tx.Commit();
                    return pointsAdded;
                }
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}
