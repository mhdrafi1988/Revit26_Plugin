// =======================================================
// File: RoofLoopReader.cs
// Location: Infrastructure/Revit/
// New in V012 — replaces the loop extraction in V011's
// RoofGeometryService, which only looked at top faces whose normal was
// exactly +Z (that is why V011 flattened the roof on launch), picked
// the outer loop by longest perimeter and restarted numbering per face.
//
// Openings now come from two read-only sources, so sloped or
// shape-edited roofs work without touching them:
//   1. Footprint roofs: the sketch profiles (largest-area loop = outer,
//      every other loop = an opening).
//   2. Every roof: holes in the top faces (HostObjectUtils.GetTopFaces) —
//      catches shaft/opening cuts that are not in the sketch. A hole is
//      a face loop that winds the opposite way to the face's outer loop.
// Duplicates between the two are merged; loops are numbered 1..n in a
// stable order (north to south, then west to east).
// Also returns the roof's existing shape-editing points (plan XY) so
// planned points that already exist are not counted or added twice.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.InnerLoopDivider.V012.Core.Geometry;
using Revit26_Plugin_Revised.InnerLoopDivider.V012.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin_Revised.InnerLoopDivider.V012.Infrastructure.Revit
{
    public sealed class RoofLoopReadResult
    {
        public List<RoofLoopModel> InnerLoops { get; } = new List<RoofLoopModel>();
        public List<P2> ExistingVertices { get; } = new List<P2>();
        public bool ShapeEditingEnabled { get; set; }
        public List<string> Notes { get; } = new List<string>();
    }

    public static class RoofLoopReader
    {
        /// <summary>5 mm in feet — loops/points closer than this are the same.</summary>
        public const double Tolerance = 5.0 / 304.8;

        public static RoofLoopReadResult Read(RoofBase roof)
        {
            if (roof == null) throw new ArgumentNullException(nameof(roof));
            var result = new RoofLoopReadResult();
            var openings = new List<(Loop2D Loop, string Source, CurveLoop Geometry)>();

            // 1 — sketch profiles (footprint roofs)
            if (roof is FootPrintRoof fp)
            {
                var sketchLoops = new List<Loop2D>();
                int n = 0;
                foreach (ModelCurveArray profile in fp.GetProfiles())
                {
                    n++;
                    var segs = new List<LoopSegment2D>();
                    foreach (ModelCurve mc in profile)
                        segs.AddRange(CurveConverter.ToSegments(mc.GeometryCurve));

                    Loop2D loop = LoopChainer.Chain(segs, Tolerance);
                    if (loop == null)
                        result.Notes.Add($"Sketch loop {n} is not closed — skipped.");
                    else
                        sketchLoops.Add(loop);
                }

                // An opening is a loop that sits inside a bigger loop. Separate
                // islands of the same roof are not openings.
                foreach (Loop2D l in sketchLoops)
                {
                    bool inside = sketchLoops.Any(o => !ReferenceEquals(o, l) && o.Area > l.Area && o.Contains(l.Samples[0]));
                    if (inside)
                        openings.Add((l, "Sketch", null));
                }
            }

            // 2 — holes in the top faces
            int faceCount = 0;
            foreach (Reference r in HostObjectUtils.GetTopFaces(roof))
            {
                if (roof.GetGeometryObjectFromReference(r) is not Face face) continue;
                faceCount++;

                var faceLoops = new List<(Loop2D Loop, CurveLoop Curves)>();
                foreach (CurveLoop cl in face.GetEdgesAsCurveLoops())
                {
                    var segs = cl.SelectMany(CurveConverter.ToSegments).ToList();
                    Loop2D loop = LoopChainer.Chain(segs, Tolerance);
                    if (loop != null && loop.Area > Tolerance * Tolerance)
                        faceLoops.Add((loop, cl));
                }

                if (faceLoops.Count < 2) continue;
                var outerFace = faceLoops.OrderByDescending(l => l.Loop.Area).First();
                int outerSign = Math.Sign(outerFace.Loop.SignedArea);
                foreach (var fl in faceLoops)
                {
                    if (ReferenceEquals(fl.Loop, outerFace.Loop) || Math.Sign(fl.Loop.SignedArea) == outerSign)
                        continue;

                    int match = openings.FindIndex(o => SameLoop(o.Loop, fl.Loop));
                    if (match >= 0)
                    {
                        if (openings[match].Geometry == null)
                            openings[match] = (openings[match].Loop, openings[match].Source, fl.Curves);
                        continue;
                    }

                    openings.Add((fl.Loop, "Opening cut", fl.Curves));
                }
            }

            if (faceCount == 0)
                result.Notes.Add("No top face found on the roof — only sketch openings were read.");

            // 3 — existing shape points
            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            result.ShapeEditingEnabled = editor != null && editor.IsEnabled;
            if (result.ShapeEditingEnabled)
            {
                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                    result.ExistingVertices.Add(CurveConverter.ToP2(v.Position));
            }
            else
            {
                result.Notes.Add("Shape editing is off on this roof — it is switched on at Apply. " +
                                 "Opening corners count as new until then.");
            }

            // 4 — stable numbering and models
            var ordered = openings
                .OrderByDescending(o => Math.Round(o.Loop.Centroid.Y / Tolerance))
                .ThenBy(o => Math.Round(o.Loop.Centroid.X / Tolerance))
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                LoopShapeInfo shape = LoopClassifier.Classify(ordered[i].Loop);
                result.InnerLoops.Add(new RoofLoopModel
                {
                    Index = i + 1,
                    LoopType = "Inner",
                    Source = ordered[i].Source,
                    Loop = ordered[i].Loop,
                    Shape = shape,
                    LoopShapeType = shape.Name,
                    IsCircular = shape.Shape == LoopShape.Circular,
                    PerimeterMm = ToMm(ordered[i].Loop.Perimeter),
                    SizeText = SizeText(shape),
                    Geometry = ordered[i].Geometry,
                    ExistingVertices = result.ExistingVertices
                });
            }

            return result;
        }

        private static bool SameLoop(Loop2D a, Loop2D b) =>
            a.Centroid.DistanceTo(b.Centroid) <= Math.Max(Tolerance * 2, 0.01 * a.Perimeter) &&
            Math.Abs(a.Perimeter - b.Perimeter) <= 0.02 * Math.Max(a.Perimeter, b.Perimeter);

        private static double ToMm(double feet) => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);

        private static string SizeText(LoopShapeInfo s) => s.Shape switch
        {
            LoopShape.Circular => $"Ø {ToMm(s.SizeA):0}",
            _ => $"{ToMm(s.SizeA):0} × {ToMm(s.SizeB):0}"
        };
    }
}
