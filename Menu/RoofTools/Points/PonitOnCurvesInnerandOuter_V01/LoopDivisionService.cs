using Autodesk.Revit.DB;
using Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Services
{
    public class LoopDivisionService
    {
        /// <summary>
        /// Adds division points to the slab shape editor for every selected loop.
        /// Routing by shape type uses the best algorithm from V2 (inner) and V3 (outer).
        /// </summary>
        public List<string> AddDivisionPoints(Document doc, RoofBase roof,
                                               IEnumerable<RoofLoopModel> loops)
        {
            var log = new List<string>();

            if (doc == null) { log.Add("❌ Document is null."); return log; }
            if (roof == null) { log.Add("❌ Roof is null."); return log; }
            if (loops == null) { log.Add("❌ Loop list is null."); return log; }

            var valid = loops.Where(l => l != null && l.IsSelected && l.RecommendedPoints > 0).ToList();
            if (!valid.Any()) { log.Add("⚠️ No loops selected for division."); return log; }

            using (var tx = new Transaction(doc, "Add Division Points — Inner & Outer"))
            {
                tx.Start();

                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled) editor.Enable();

                bool fatalError = false;

                foreach (var loop in valid)
                {
                    if (loop.Geometry == null)
                    {
                        log.Add($"⚠️ Loop {loop.Index} [{loop.LoopType} / {loop.LoopShapeType}]: Skipped — geometry is null.");
                        continue;
                    }

                    try
                    {
                        int added = DispatchPoints(editor, loop, log);
                        log.Add($"✅ Loop {loop.Index} [{loop.LoopType} / {loop.LoopShapeType}]: {added} point(s) added.");
                    }
                    catch (Exception ex)
                    {
                        log.Add($"❌ Loop {loop.Index} [{loop.LoopType} / {loop.LoopShapeType}]: {ex.Message}");
                        log.Add("❌ Transaction rolled back — no points were placed.");
                        fatalError = true;
                        break;
                    }
                }

                if (fatalError)
                    tx.RollBack();
                else
                    tx.Commit();
            }

            return log;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Route to correct algorithm by shape type
        // ─────────────────────────────────────────────────────────────────────────
        private int DispatchPoints(SlabShapeEditor editor, RoofLoopModel loop, List<string> log)
        {
            switch (loop.LoopShapeType)
            {
                case "Circular":
                    return AddCircularPoints(editor, loop, log);

                case "Oval":
                    return AddMixedCurvedPoints(editor, loop, log, closedCurve: true);

                case "Arc":
                    return AddMixedCurvedPoints(editor, loop, log, closedCurve: false);

                default:   // Rectangle / Other
                    return AddParametricPoints(editor, loop, log);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // CIRCULAR: evenly by angle around the circle centre
        // ─────────────────────────────────────────────────────────────────────────
        private int AddCircularPoints(SlabShapeEditor editor, RoofLoopModel loop, List<string> log)
        {
            int n = loop.RecommendedPoints;
            XYZ center = loop.Center;
            double radius = loop.Radius;

            double z = center.Z;
            var firstCurve = loop.Geometry.FirstOrDefault();
            if (firstCurve != null) z = firstCurve.GetEndPoint(0).Z;

            double step = 2 * Math.PI / n;
            int added = 0;

            for (int i = 0; i < n; i++)
            {
                double angle = i * step;
                XYZ pt = new XYZ(center.X + radius * Math.Cos(angle),
                                       center.Y + radius * Math.Sin(angle),
                                       z);
                added += TryAdd(editor, pt, loop.Index, $"angle {Math.Round(angle * 180 / Math.PI, 1)}°", log);
            }

            return added;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // OVAL / ARC: proportional spread across all segments
        //   closedCurve=true  → arcs use angle-based distribution
        //   closedCurve=false → arcs use parametric (arc-length) distribution
        // ─────────────────────────────────────────────────────────────────────────
        private int AddMixedCurvedPoints(SlabShapeEditor editor, RoofLoopModel loop,
                                          List<string> log, bool closedCurve)
        {
            int n = loop.RecommendedPoints;
            int added = 0;
            var curves = loop.Geometry.ToList();
            double totalLength = curves.Sum(c => c.Length);
            if (totalLength < 1e-9) return 0;

            foreach (var curve in curves)
            {
                double fraction = curve.Length / totalLength;
                int ptsOnCurve = Math.Max(1, (int)Math.Round(n * fraction));

                if (curve is Arc arc && closedCurve)
                {
                    XYZ arcCenter = arc.Center;
                    double z = arc.GetEndPoint(0).Z;
                    XYZ startVec = (arc.GetEndPoint(0) - arcCenter).Normalize();
                    double startAngle = Math.Atan2(startVec.Y, startVec.X);
                    XYZ endVec = (arc.GetEndPoint(1) - arcCenter).Normalize();
                    double endAngle = Math.Atan2(endVec.Y, endVec.X);

                    double sweep = endAngle - startAngle;
                    XYZ midPt = arc.Evaluate(0.5, true);
                    XYZ midVec = (midPt - arcCenter).Normalize();
                    double midGeom = Math.Atan2(midVec.Y, midVec.X);
                    double midCalc = startAngle + sweep / 2.0;
                    double diff = NormalizeAngle(midGeom - midCalc);
                    if (Math.Abs(diff) > 0.1)
                        sweep = sweep > 0 ? sweep - 2 * Math.PI : sweep + 2 * Math.PI;

                    double arcR = arc.Radius;
                    double aStep = sweep / (ptsOnCurve + 1.0);

                    for (int i = 1; i <= ptsOnCurve; i++)
                    {
                        double angle = startAngle + i * aStep;
                        XYZ pt = new XYZ(arcCenter.X + arcR * Math.Cos(angle),
                                               arcCenter.Y + arcR * Math.Sin(angle),
                                               z);
                        added += TryAdd(editor, pt, loop.Index,
                                        $"arc {Math.Round(angle * 180 / Math.PI, 1)}°", log);
                    }
                }
                else
                {
                    for (int i = 1; i <= ptsOnCurve; i++)
                    {
                        double param = i / (ptsOnCurve + 1.0);
                        XYZ pt = curve.Evaluate(param, true);
                        added += TryAdd(editor, pt, loop.Index, $"param {Math.Round(param, 3)}", log);
                    }
                }
            }

            return added;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // RECTANGLE / OTHER: parametric spread proportional to segment length
        // ─────────────────────────────────────────────────────────────────────────
        private int AddParametricPoints(SlabShapeEditor editor, RoofLoopModel loop, List<string> log)
        {
            int n = loop.RecommendedPoints;
            int added = 0;
            var curves = loop.Geometry.ToList();
            double totalLength = curves.Sum(c => c.Length);

            foreach (var curve in curves)
            {
                double fraction = curve.Length / totalLength;
                int ptsOnCurve = Math.Max(1, (int)Math.Round(n * fraction));

                for (int i = 1; i <= ptsOnCurve; i++)
                {
                    double param = i / (ptsOnCurve + 1.0);
                    XYZ pt = curve.Evaluate(param, true);
                    added += TryAdd(editor, pt, loop.Index, $"param {Math.Round(param, 3)}", log);
                }
            }

            return added;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────
        private int TryAdd(SlabShapeEditor editor, XYZ pt, int idx, string label, List<string> log)
        {
            try
            {
                editor.AddPoint(pt);
                return 1;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                log.Add($"  ↳ Loop {idx}: Point at {label} already exists — skipped.");
                return 0;
            }
        }

        private double NormalizeAngle(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}