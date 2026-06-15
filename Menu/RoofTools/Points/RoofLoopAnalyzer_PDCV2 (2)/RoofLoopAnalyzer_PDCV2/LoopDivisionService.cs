using Autodesk.Revit.DB;
using Revit26_Plugin.PDCV2.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PDCV2.Services
{
    public class LoopDivisionService
    {
        /// <summary>
        /// Adds division points to the roof slab shape editor for each selected loop.
        /// Returns a list of log messages describing success or failure per loop.
        /// </summary>
        public List<string> AddDivisionPoints(Document doc, RoofBase roof, IEnumerable<RoofLoopModel> loops)
        {
            var log = new List<string>();

            if (doc == null)  { log.Add("❌ Error: Document is null.");  return log; }
            if (roof == null) { log.Add("❌ Error: Roof is null.");       return log; }
            if (loops == null){ log.Add("❌ Error: Loop list is null.");  return log; }

            var validLoops = loops.Where(l => l != null && l.IsSelected && l.RecommendedPoints > 0).ToList();

            if (!validLoops.Any())
            {
                log.Add("⚠️ No loops selected for division.");
                return log;
            }

            using (Transaction tx = new Transaction(doc, "Add Division Points"))
            {
                tx.Start();

                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled)
                    editor.Enable();

                foreach (var loop in validLoops)
                {
                    if (loop.Geometry == null)
                    {
                        log.Add($"⚠️ Loop {loop.Index} ({loop.LoopShapeType}): Skipped — geometry is null.");
                        continue;
                    }

                    try
                    {
                        int pointsAdded = 0;

                        if (loop.LoopShapeType == "Circular")
                        {
                            pointsAdded = AddCircularPoints(editor, loop, log);
                        }
                        else
                        {
                            pointsAdded = AddParametricPoints(editor, loop, log);
                        }

                        log.Add($"✅ Loop {loop.Index} ({loop.LoopShapeType}): {pointsAdded} point(s) added.");
                    }
                    catch (Exception ex)
                    {
                        log.Add($"❌ Loop {loop.Index} ({loop.LoopShapeType}): Exception — {ex.Message}");
                    }
                }

                tx.Commit();
            }

            return log;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // CIRCULAR: distribute n points evenly by angle around the circle center
        // ─────────────────────────────────────────────────────────────────────────
        private int AddCircularPoints(SlabShapeEditor editor, RoofLoopModel loop, List<string> log)
        {
            int n          = loop.RecommendedPoints;
            XYZ center     = loop.Center;
            double radius  = loop.Radius;
            double z       = center.Z;

            // Determine the Z level from the first curve of the geometry
            // (center.Z from arcs may be at a different elevation than the face)
            var firstCurve = loop.Geometry.FirstOrDefault();
            if (firstCurve != null)
                z = firstCurve.GetEndPoint(0).Z;

            double angleStep = 2 * Math.PI / n;   // e.g. 8 points → 45° apart
            int pointsAdded  = 0;

            for (int i = 0; i < n; i++)
            {
                double angle = i * angleStep;
                double x     = center.X + radius * Math.Cos(angle);
                double y     = center.Y + radius * Math.Sin(angle);
                XYZ point    = new XYZ(x, y, z);

                try
                {
                    editor.AddPoint(point);
                    pointsAdded++;
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    log.Add($"  ↳ Loop {loop.Index}: Point at angle {Math.Round(angle * 180 / Math.PI, 1)}° already exists — skipped.");
                }
            }

            return pointsAdded;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // RECTANGLE / OTHER: distribute n points evenly along each curve by parameter
        // ─────────────────────────────────────────────────────────────────────────
        private int AddParametricPoints(SlabShapeEditor editor, RoofLoopModel loop, List<string> log)
        {
            int n           = loop.RecommendedPoints;
            int pointsAdded = 0;

            var curves = loop.Geometry.ToList();
            if (!curves.Any()) return 0;

            // Spread n points evenly across all curves using their arc-length proportions
            double totalLength = curves.Sum(c => c.Length);

            foreach (Curve curve in curves)
            {
                // How many points belong proportionally on this curve
                double fraction      = curve.Length / totalLength;
                int pointsOnCurve    = Math.Max(1, (int)Math.Round(n * fraction));

                for (int i = 0; i < pointsOnCurve; i++)
                {
                    // Distribute across the interior of the curve (exclude endpoints to avoid duplicates at joins)
                    double param = (i + 1.0) / (pointsOnCurve + 1.0);
                    XYZ point    = curve.Evaluate(param, true);

                    try
                    {
                        editor.AddPoint(point);
                        pointsAdded++;
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                    {
                        log.Add($"  ↳ Loop {loop.Index}: Point at param {Math.Round(param, 3)} already exists — skipped.");
                    }
                }
            }

            return pointsAdded;
        }
    }
}
