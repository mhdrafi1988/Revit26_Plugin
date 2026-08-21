using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>Result of extracting boundary loops from a single Floor/Roof element.</summary>
    public class ExtractedProfile
    {
        public long SourceElementId { get; set; }
        public List<List<Curve>> OuterLoops { get; set; } = new();
        public List<List<Curve>> InnerLoops { get; set; } = new();

        /// <summary>Debug detail about which face was picked as the plan-view
        /// footprint (see FindTopHorizontalFace) — surfaced by the engines as a
        /// Debug-severity log line, not used in geometry output.</summary>
        public double SourceFaceArea { get; set; }
        public double SourceFaceNormalZ { get; set; }

        /// <summary>"SolidGeometry", "ProfileBased", or "SolidGeometry (Profile-Based
        /// unavailable)" when Profile-Based was requested but the element had no
        /// usable sketch. Debug-log detail only, not used in geometry output.</summary>
        public string ExtractionMethodUsed { get; set; } = "SolidGeometry";
    }

    /// <summary>
    /// Extracts Profile-group (Floor/Roof) boundary geometry, per spec Sections 16–17.
    ///
    /// Curve-type handling (per design discussion with Rafi):
    /// - Line   → kept as-is, exact.
    /// - Arc    → kept as-is, exact (3-point/center-radius definition already exact —
    ///            no sampling). Full-circle arcs are never produced by EdgeLoops (a
    ///            circular floor edge is still a bounded arc segment along the loop),
    ///            but the 2π-sweep split rule is applied defensively if ever encountered.
    /// - Ellipse (incl. partial) → kept as-is, exact.
    /// - Anything else (HermiteSpline/NurbSpline) → tessellated via Curve.Tessellate()
    ///   and rebuilt as HermiteSpline UNLESS ComplexCurveSettings.ReplaceWithFallback
    ///   is enabled, in which case replaced by StraightChord or BestFitArc.
    /// No curve type is manually sampled into a polyline of many short Lines — this
    /// keeps element count low and matches the "clean 2D output" requirement.
    /// </summary>
    public class GeometryExtractionService
    {
        /// <summary>
        /// Extracts outer + inner boundary loops from a Floor or Roof element's
        /// top-most horizontal face, in the LINKED document's own coordinate system
        /// (transform to host happens in GeometryTransformService, kept as a separate
        /// step per the pipeline architecture in spec Section 29).
        /// </summary>
        public ExtractedProfile? ExtractProfile(
            Element element,
            ComplexCurveSettings complexCurveSettings,
            Action<string, Autodesk.Revit.DB.ElementId?>? onWarning = null)
        {
            var profile = new ExtractedProfile { SourceElementId = element.Id.Value };

            Options geomOptions = new() { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement? geomElem;
            try
            {
                geomElem = element.get_Geometry(geomOptions);
            }
            catch (Exception ex)
            {
                onWarning?.Invoke($"Geometry extraction failed: {ex.Message}", element.Id);
                return null;
            }
            if (geomElem == null)
            {
                onWarning?.Invoke("No geometry returned for element.", element.Id);
                return null;
            }

            Face? bestFace = FindTopHorizontalFace(geomElem);
            if (bestFace == null)
            {
                onWarning?.Invoke("No horizontal planar/analytic face found — cannot extract 2D boundary.", element.Id);
                return null;
            }

            if (bestFace is PlanarFace bestPlanar)
            {
                profile.SourceFaceArea = bestPlanar.Area;
                profile.SourceFaceNormalZ = bestPlanar.FaceNormal.Normalize().Z;
            }

            // face.EdgeLoops (not GetEdgesAsCurveLoops — doesn't exist, per project notes)
            EdgeArrayArray loops = bestFace.EdgeLoops;
            if (loops.Size == 0)
            {
                onWarning?.Invoke("Face has no edge loops.", element.Id);
                return null;
            }

            // Determine outer vs inner loops by signed area (largest |area| = outer,
            // per standard convention; Revit doesn't explicitly flag which loop is outer).
            var loopCurveLists = new List<(List<Curve> curves, double signedArea)>();

            for (int i = 0; i < loops.Size; i++)
            {
                EdgeArray edgeArray = loops.get_Item(i);
                var curves = new List<Curve>();

                for (int j = 0; j < edgeArray.Size; j++)
                {
                    Edge edge = edgeArray.get_Item(j);
                    Curve rawCurve = edge.AsCurve();
                    Curve processed = ProcessCurve(rawCurve, complexCurveSettings, element.Id, onWarning);
                    curves.Add(processed);
                }

                double area = ComputeSignedArea(curves);
                loopCurveLists.Add((curves, area));
            }

            if (loopCurveLists.Count == 0) return null;

            var outer = loopCurveLists.OrderByDescending(l => System.Math.Abs(l.signedArea)).First();
            profile.OuterLoops.Add(outer.curves);

            foreach (var inner in loopCurveLists.Where(l => l.curves != outer.curves))
                profile.InnerLoops.Add(inner.curves);

            return profile;
        }

        /// <summary>
        /// Profile-Based extraction (ProcessingScope's per-mapping ExtractionMethod):
        /// reads the element's own sketch/profile curves (Autodesk.Revit.DB.Sketch,
        /// found via GetDependentElements) instead of analyzing solid geometry — the
        /// curves the user actually drew when sketching the Floor/Roof boundary,
        /// rather than a re-derived face. Outer/inner loop selection (largest |area|
        /// = outer) uses the same convention as ExtractProfile.
        ///
        /// Falls back to ExtractProfile (Solid Geometry) automatically, logged, when
        /// no sketch is found (non-sketch-based Roof types, in-place families, etc.)
        /// — per Rafi's explicit choice: still produce a boundary rather than skip
        /// the element outright.
        /// </summary>
        public ExtractedProfile? ExtractProfileFromSketch(
            Element element,
            ComplexCurveSettings complexCurveSettings,
            Action<string, Autodesk.Revit.DB.ElementId?>? onWarning = null,
            Action<string, Autodesk.Revit.DB.ElementId?>? onFallbackNotice = null)
        {
            Sketch? sketch = null;
            try
            {
                ElementId sketchId = element
                    .GetDependentElements(new ElementClassFilter(typeof(Sketch)))
                    .FirstOrDefault();
                if (sketchId != null)
                    sketch = element.Document.GetElement(sketchId) as Sketch;
            }
            catch (Exception ex)
            {
                onFallbackNotice?.Invoke($"Sketch lookup failed ({ex.Message}) — falling back to Solid Geometry.", element.Id);
            }

            if (sketch?.Profile == null || sketch.Profile.IsEmpty)
            {
                onFallbackNotice?.Invoke("No sketch/profile available for Profile-Based extraction — falling back to Solid Geometry.", element.Id);
                ExtractedProfile? fallback = ExtractProfile(element, complexCurveSettings, onWarning);
                if (fallback != null) fallback.ExtractionMethodUsed = "SolidGeometry (Profile-Based unavailable)";
                return fallback;
            }

            var profile = new ExtractedProfile { SourceElementId = element.Id.Value, ExtractionMethodUsed = "ProfileBased" };
            var loopCurveLists = new List<(List<Curve> curves, double signedArea)>();

            foreach (CurveArray curveArray in sketch.Profile)
            {
                var curves = new List<Curve>();
                foreach (Curve rawCurve in curveArray)
                {
                    Curve processed = ProcessCurve(rawCurve, complexCurveSettings, element.Id, onWarning);
                    curves.Add(processed);
                }
                if (curves.Count == 0) continue;

                double area = ComputeSignedArea(curves);
                loopCurveLists.Add((curves, area));
            }

            if (loopCurveLists.Count == 0)
            {
                onFallbackNotice?.Invoke("Sketch had no usable profile loops — falling back to Solid Geometry.", element.Id);
                ExtractedProfile? fallback = ExtractProfile(element, complexCurveSettings, onWarning);
                if (fallback != null) fallback.ExtractionMethodUsed = "SolidGeometry (Profile-Based unavailable)";
                return fallback;
            }

            var outer = loopCurveLists.OrderByDescending(l => System.Math.Abs(l.signedArea)).First();
            profile.OuterLoops.Add(outer.curves);

            foreach (var inner in loopCurveLists.Where(l => l.curves != outer.curves))
                profile.InnerLoops.Add(inner.curves);

            return profile;
        }

        /// <summary>
        /// Picks the topmost horizontal (or near-horizontal, tolerant of slight roof
        /// slope) planar face — the face whose outward normal has the largest positive
        /// Z component among candidate faces. For sloped roofs this is still the single
        /// face that best represents the plan-view footprint; multi-face roofs (hip/
        /// gable, multiple slab shape regions) pick the largest such face by area.
        /// Flagged: multi-face roof handling with SEPARATE Detail Lines per face-plane
        /// is a Phase 3+ refinement if a single dominant face proves insufficient in
        /// testing on your real project roofs.
        /// </summary>
        private Face? FindTopHorizontalFace(GeometryElement geomElem)
        {
            Face? best = null;
            double bestScore = double.MinValue;

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Volume > 1e-9)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is not PlanarFace planar) continue;
                        double zComponent = planar.FaceNormal.Normalize().Z;
                        if (zComponent <= 0.1) continue; // skip downward/vertical faces

                        double area = face.Area;
                        double score = zComponent * area; // favor flat + large
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = face;
                        }
                    }
                }
                else if (obj is GeometryInstance gi)
                {
                    var nested = gi.GetInstanceGeometry();
                    var nestedBest = FindTopHorizontalFace(nested);
                    if (nestedBest != null)
                    {
                        // Can't easily compare cross-scope score without re-deriving;
                        // accept first nested candidate found if no top-level face exists.
                        best ??= nestedBest;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Applies the exact-reconstruction-vs-tessellation decision for a single edge
        /// curve. Line/Arc/Ellipse pass through unchanged (already exact analytic
        /// curves owned by Revit). Anything else is tessellated and rebuilt.
        /// </summary>
        /// <summary>
        /// Applies the exact-reconstruction-vs-tessellation decision for a single edge
        /// curve. Line/Arc/Ellipse pass through unchanged (already exact analytic
        /// curves owned by Revit). Anything else is tessellated and rebuilt.
        /// Public (as NormalizeSingleCurve) so LinearGeometryExtractionService can
        /// reuse the same logic for Wall/Beam centerlines without duplicating it.
        /// </summary>
        public Curve NormalizeSingleCurve(
            Curve rawCurve,
            ComplexCurveSettings settings,
            ElementId sourceElementId,
            Action<string, ElementId?>? onWarning)
            => ProcessCurve(rawCurve, settings, sourceElementId, onWarning);

        private Curve ProcessCurve(
            Curve rawCurve,
            ComplexCurveSettings settings,
            ElementId sourceElementId,
            Action<string, ElementId?>? onWarning)
        {
            switch (rawCurve)
            {
                case Line:
                case Arc:
                case Ellipse:
                    return rawCurve; // exact, no sampling needed

                default:
                    return HandleComplexCurve(rawCurve, settings, sourceElementId, onWarning);
            }
        }

        /// <summary>
        /// Non-analytic curve (spline/NURBS edge). Default: tessellate and rebuild as
        /// HermiteSpline (closest to true shape, single element, not chopped into many
        /// Line segments). If ReplaceWithFallback is on: StraightChord (single Line
        /// start→end) or BestFitArc (Arc through start/mid/end, degrades to
        /// StraightChord automatically if curvature sign is inconsistent along the
        /// curve — a flip indicates an S-curve/inflection an Arc cannot represent).
        /// </summary>
        private Curve HandleComplexCurve(
            Curve rawCurve,
            ComplexCurveSettings settings,
            ElementId sourceElementId,
            Action<string, ElementId?>? onWarning)
        {
            XYZ start = rawCurve.GetEndPoint(0);
            XYZ end = rawCurve.GetEndPoint(1);

            if (settings.ReplaceWithFallback)
            {
                if (settings.FallbackShape == SplineFallbackShape.StraightChord)
                {
                    onWarning?.Invoke("Complex curve replaced with straight chord (Complex Curve Handling setting).", sourceElementId);
                    return Line.CreateBound(start, end);
                }

                // BestFitArc
                XYZ mid = rawCurve.Evaluate(0.5, true);
                if (TryFitArc(start, mid, end, out Arc? arc) && arc != null && CurvatureSignConsistent(rawCurve))
                {
                    onWarning?.Invoke("Complex curve replaced with best-fit arc (Complex Curve Handling setting).", sourceElementId);
                    return arc;
                }

                onWarning?.Invoke("Best-fit arc unsuitable (inconsistent curvature/inflection) — fell back to straight chord.", sourceElementId);
                return Line.CreateBound(start, end);
            }

            // Default: tessellate and rebuild as a single HermiteSpline.
            IList<XYZ> tessPoints = rawCurve.Tessellate();
            if (tessPoints.Count < 2)
            {
                onWarning?.Invoke("Complex curve tessellation returned too few points — using straight chord instead.", sourceElementId);
                return Line.CreateBound(start, end);
            }
            if (tessPoints.Count == 2)
            {
                return Line.CreateBound(tessPoints[0], tessPoints[1]);
            }

            try
            {
                return HermiteSpline.Create(tessPoints, false);
            }
            catch (Exception ex)
            {
                onWarning?.Invoke($"HermiteSpline reconstruction failed ({ex.Message}) — using straight chord instead.", sourceElementId);
                return Line.CreateBound(start, end);
            }
        }

        private bool TryFitArc(XYZ start, XYZ mid, XYZ end, out Arc? arc)
        {
            try
            {
                arc = Arc.Create(start, end, mid);
                return true;
            }
            catch
            {
                arc = null;
                return false;
            }
        }

        /// <summary>
        /// Coarse curvature-sign check: samples a few points along the curve and
        /// verifies the cross-product turning direction doesn't flip sign, which
        /// would indicate an inflection point an Arc's constant curvature cannot
        /// represent. Not a rigorous curvature analysis — a fast, defensible gate.
        /// </summary>
        private bool CurvatureSignConsistent(Curve curve)
        {
            const int samples = 5;
            double? lastSign = null;

            XYZ? p0 = null, p1 = null;
            for (int i = 0; i <= samples; i++)
            {
                double t = (double)i / samples;
                XYZ p2 = curve.Evaluate(t, true);

                if (p0 != null && p1 != null)
                {
                    XYZ v1 = p1 - p0;
                    XYZ v2 = p2 - p1;
                    double cross = v1.X * v2.Y - v1.Y * v2.X;
                    if (System.Math.Abs(cross) > 1e-9)
                    {
                        double sign = System.Math.Sign(cross);
                        if (lastSign.HasValue && sign != lastSign.Value)
                            return false;
                        lastSign = sign;
                    }
                }
                p0 = p1;
                p1 = p2;
            }
            return true;
        }

        /// <summary>Shoelace formula on curve tessellation points, used only to rank
        /// loops by enclosed area (outer = largest |area|) — not used for geometry output.</summary>
        private double ComputeSignedArea(List<Curve> curves)
        {
            var pts = new List<XYZ>();
            foreach (var c in curves)
                pts.Add(c.GetEndPoint(0));

            double area = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p1 = pts[i];
                var p2 = pts[(i + 1) % pts.Count];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            return area / 2.0;
        }
    }
}
