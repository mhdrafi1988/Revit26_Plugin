using Autodesk.Revit.DB;
using Revit26_Plugin.PDCV3.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PDCV3.Services
{
    public class LoopDivisionService
    {
        /// <summary>
        /// Adds division points to the roof slab shape editor for each selected loop.
        /// Targets OUTER loops. Curved segments use angle-based (closed) or arc-length
        /// (open arc) distribution; straight segments use parametric distribution.
        /// </summary>
        public List<string> AddDivisionPoints(Document doc, RoofBase roof, IEnumerable<RoofLoopModel> loops)
        {
            var log = new List<string>();

            if (doc  == null) { log.Add("❌ Error: Document is null.");  return log; }
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

                        switch (loop.LoopShapeType)
                        {
                            case "Circular":
                                pointsAdded = AddCircularPoints(editor, loop, log);
                                break;

                            case "Oval":
                                // Oval = closed curved boundary → angle-based on arcs, parametric on lines
                                pointsAdded = AddMixedCurvedPoints(editor, loop, log, closedCurve: true);
                                break;

                            case "Arc":
                                // Open arc segments mixed with lines → arc-length proportional
                                pointsAdded = AddMixedCurvedPoints(editor, loop, log, closedCurve: false);
                                break;

                            default:
                                // Rectangle / Other → parametric spread across all segments
                                pointsAdded = AddParametricPoints(editor, loop, log);
                                break;
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
            int    n      = loop.RecommendedPoints;
            XYZ    center = loop.Center;
            double radius = loop.Radius;

            double z = center.Z;
            var firstCurve = loop.Geometry.FirstOrDefault();
            if (firstCurve != null)
                z = firstCurve.GetEndPoint(0).Z;

            double angleStep = 2 * Math.PI / n;
            int pointsAdded  = 0;

            for (int i = 0; i < n; i++)
            {
                double angle = i * angleStep;
                XYZ    pt    = new XYZ(center.X + radius * Math.Cos(angle),
                                       center.Y + radius * Math.Sin(angle),
                                       z);
                pointsAdded += TryAddPoint(editor, pt, loop.Index, $"angle {Math.Round(angle * 180 / Math.PI, 1)}°", log);
            }

            return pointsAdded;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // OVAL / ARC: mixed curved + straight segments
        //   closedCurve=true  → arc segments use angle-based distribution
        //   closedCurve=false → arc segments use arc-length proportional distribution
        //   ALL segments (curved and straight) receive points proportional to length
        // ─────────────────────────────────────────────────────────────────────────
        private int AddMixedCurvedPoints(SlabShapeEditor editor, RoofLoopModel loop,
                                          List<string> log, bool closedCurve)
        {
            int    n           = loop.RecommendedPoints;
            int    pointsAdded = 0;
            var    curves      = loop.Geometry.ToList();
            double totalLength = curves.Sum(c => c.Length);

            if (totalLength < 1e-9) return 0;

            foreach (var curve in curves)
            {
                double fraction     = curve.Length / totalLength;
                int    ptsOnCurve   = Math.Max(1, (int)Math.Round(n * fraction));

                if (curve is Arc arc && closedCurve)
                {
                    // Angle-based: compute arc's own center and sweep
                    XYZ    arcCenter = arc.Center;
                    double z         = arc.GetEndPoint(0).Z;
                    XYZ    startVec  = (arc.GetEndPoint(0) - arcCenter).Normalize();
                    double startAngle = Math.Atan2(startVec.Y, startVec.X);
                    XYZ    endVec    = (arc.GetEndPoint(1) - arcCenter).Normalize();
                    double endAngle  = Math.Atan2(endVec.Y, endVec.X);

                    // Normalize sweep direction (always go the short way the arc travels)
                    double sweep = endAngle - startAngle;
                    // Adjust sweep so it matches actual arc direction
                    // Use arc midpoint to determine sign
                    XYZ midPt    = arc.Evaluate(0.5, true);
                    XYZ midVec   = (midPt - arcCenter).Normalize();
                    double midAngleGeom = Math.Atan2(midVec.Y, midVec.X);
                    double midAngleCalc = startAngle + sweep / 2.0;
                    // If mid doesn't match, flip sweep
                    double diff = NormalizeAngle(midAngleGeom - midAngleCalc);
                    if (Math.Abs(diff) > 0.1)
                        sweep = sweep > 0 ? sweep - 2 * Math.PI : sweep + 2 * Math.PI;

                    double arcRadius  = arc.Radius;
                    double angleStep  = sweep / (ptsOnCurve + 1.0);

                    for (int i = 1; i <= ptsOnCurve; i++)
                    {
                        double angle = startAngle + i * angleStep;
                        XYZ    pt    = new XYZ(arcCenter.X + arcRadius * Math.Cos(angle),
                                               arcCenter.Y + arcRadius * Math.Sin(angle),
                                               z);
                        pointsAdded += TryAddPoint(editor, pt, loop.Index,
                                                    $"arc angle {Math.Round(angle * 180 / Math.PI, 1)}°", log);
                    }
                }
                else
                {
                    // Arc-length / parametric: works for both Line and Arc segments
                    for (int i = 1; i <= ptsOnCurve; i++)
                    {
                        double param = i / (ptsOnCurve + 1.0);
                        XYZ    pt    = curve.Evaluate(param, true);
                        pointsAdded += TryAddPoint(editor, pt, loop.Index,
                                                    $"param {Math.Round(param, 3)}", log);
                    }
                }
            }

            return pointsAdded;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // RECTANGLE / OTHER: distribute n points evenly across all curves
        // ─────────────────────────────────────────────────────────────────────────
        private int AddParametricPoints(SlabShapeEditor editor, RoofLoopModel loop, List<string> log)
        {
            int    n           = loop.RecommendedPoints;
            int    pointsAdded = 0;
            var    curves      = loop.Geometry.ToList();
            double totalLength = curves.Sum(c => c.Length);

            foreach (var curve in curves)
            {
                double fraction   = curve.Length / totalLength;
                int    ptsOnCurve = Math.Max(1, (int)Math.Round(n * fraction));

                for (int i = 1; i <= ptsOnCurve; i++)
                {
                    double param = i / (ptsOnCurve + 1.0);
                    XYZ    pt    = curve.Evaluate(param, true);
                    pointsAdded += TryAddPoint(editor, pt, loop.Index, $"param {Math.Round(param, 3)}", log);
                }
            }

            return pointsAdded;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Shared: try AddPoint, catch duplicate
        // ─────────────────────────────────────────────────────────────────────────
        private int TryAddPoint(SlabShapeEditor editor, XYZ pt, int loopIdx, string label, List<string> log)
        {
            try
            {
                editor.AddPoint(pt);
                return 1;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                log.Add($"  ↳ Loop {loopIdx}: Point at {label} already exists — skipped.");
                return 0;
            }
        }

        private double NormalizeAngle(double angle)
        {
            while (angle >  Math.PI) angle -= 2 * Math.PI;
            while (angle < -Math.PI) angle += 2 * Math.PI;
            return angle;
        }
    }
}
