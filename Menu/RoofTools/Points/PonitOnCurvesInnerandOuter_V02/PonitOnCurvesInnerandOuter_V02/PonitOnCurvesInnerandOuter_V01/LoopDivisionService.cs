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
        /// Routing by shape type:
        ///   Circular / Oval / Arc  → arc-only algorithm: N evenly-spaced points PER ARC SEGMENT
        ///                            (straight line segments are skipped).
        ///   Rectangle / Other      → parametric spread proportional to segment length (unchanged).
        /// </summary>
        public List<string> AddDivisionPoints(Document doc, RoofBase roof,
                                               IEnumerable<RoofLoopModel> loops)
        {
            var log = new List<string>();

            if (doc   == null) { log.Add("❌ Document is null.");  return log; }
            if (roof  == null) { log.Add("❌ Roof is null.");       return log; }
            if (loops == null) { log.Add("❌ Loop list is null.");  return log; }

            var valid = loops.Where(l => l != null && l.IsSelected && l.RecommendedPoints > 0).ToList();
            if (!valid.Any()) { log.Add("⚠️ No loops selected for division."); return log; }

            using (var tx = new Transaction(doc, "Add Division Points — Arc Boundaries"))
            {
                tx.Start();

                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled) editor.Enable();

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
                    }
                }

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
                case "Oval":
                case "Arc":
                    return AddArcOnlyPoints(editor, loop, log);

                default:   // Rectangle / Other — parametric spread unchanged
                    return AddParametricPoints(editor, loop, log);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ARC-ONLY: N evenly-spaced points per arc segment (lines skipped).
        //
        // Strategy:
        //   1. Collect only Arc segments from the loop geometry.
        //   2. Distribute RecommendedPoints across arcs proportionally by arc length.
        //   3. For each arc place its share of points at equal angular steps,
        //      NOT including the endpoints (those are existing Revit vertices).
        // ─────────────────────────────────────────────────────────────────────────
        private int AddArcOnlyPoints(SlabShapeEditor editor, RoofLoopModel loop, List<string> log)
        {
            var arcs = loop.Geometry.OfType<Arc>().ToList();

            if (!arcs.Any())
            {
                log.Add($"  ↳ Loop {loop.Index}: No arc segments found — skipped.");
                return 0;
            }

            int    n          = loop.RecommendedPoints;
            double totalArcL  = arcs.Sum(a => a.Length);
            int    added      = 0;

            foreach (Arc arc in arcs)
            {
                // Points allocated to this arc segment, proportional to its length
                double fraction   = arc.Length / totalArcL;
                int    ptsOnArc   = Math.Max(1, (int)Math.Round(n * fraction));

                // Compute signed sweep angle of this arc
                XYZ    arcCenter  = arc.Center;
                double z          = arc.GetEndPoint(0).Z;
                double radius     = arc.Radius;

                XYZ    startVec   = (arc.GetEndPoint(0) - arcCenter).Normalize();
                XYZ    endVec     = (arc.GetEndPoint(1) - arcCenter).Normalize();
                double startAngle = Math.Atan2(startVec.Y, startVec.X);
                double endAngle   = Math.Atan2(endVec.Y,   endVec.X);

                // Resolve sweep direction using arc midpoint
                double sweep = endAngle - startAngle;
                XYZ    midPt     = arc.Evaluate(0.5, normalized: true);
                XYZ    midVec    = (midPt - arcCenter).Normalize();
                double midGeom   = Math.Atan2(midVec.Y, midVec.X);
                double midCalc   = startAngle + sweep / 2.0;
                double diff      = NormalizeAngle(midGeom - midCalc);
                if (Math.Abs(diff) > 0.1)
                    sweep = sweep > 0 ? sweep - 2 * Math.PI : sweep + 2 * Math.PI;

                // Place ptsOnArc interior points: step = sweep / (ptsOnArc + 1)
                // i = 1 … ptsOnArc so we never place at start or end (existing vertices)
                double step = sweep / (ptsOnArc + 1.0);

                for (int i = 1; i <= ptsOnArc; i++)
                {
                    double angle = startAngle + i * step;
                    XYZ    pt    = new XYZ(
                        arcCenter.X + radius * Math.Cos(angle),
                        arcCenter.Y + radius * Math.Sin(angle),
                        z);

                    string label = $"arc {Math.Round(angle * 180.0 / Math.PI, 1)}°";
                    added += TryAdd(editor, pt, loop.Index, label, log);
                }
            }

            return added;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // RECTANGLE / OTHER: parametric spread proportional to segment length
        // ─────────────────────────────────────────────────────────────────────────
        private int AddParametricPoints(SlabShapeEditor editor, RoofLoopModel loop, List<string> log)
        {
            int    n           = loop.RecommendedPoints;
            int    added       = 0;
            var    curves      = loop.Geometry.ToList();
            double totalLength = curves.Sum(c => c.Length);

            foreach (var curve in curves)
            {
                double fraction   = curve.Length / totalLength;
                int    ptsOnCurve = Math.Max(1, (int)Math.Round(n * fraction));

                for (int i = 1; i <= ptsOnCurve; i++)
                {
                    double param = i / (ptsOnCurve + 1.0);
                    XYZ    pt    = curve.Evaluate(param, normalized: true);
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
            while (a >  Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}
