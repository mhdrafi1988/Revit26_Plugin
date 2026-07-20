using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005
{
    /// <summary>
    /// V005 correction: there is no modern FootPrintRoof.Create(IList&lt;CurveLoop&gt;, ...)
    /// static API — confirmed against the Revit API docs through the 2026 release.
    /// doc.Create.NewFootPrintRoof(CurveArray, Level, RoofType, out ModelCurveArray)
    /// remains the only footprint-roof creation API, and is what this service (and V005
    /// before it) actually calls. The V005 header comment claiming a switch to a
    /// "modern Create API" was incorrect and has been removed.
    ///
    /// Geometric boundary validation (closed loop, planarity, curve-type restriction,
    /// BoundaryValidation.IsValidHorizontalBoundary) now happens upstream in
    /// RoofBoundaryValidationService, called by the handler before this method runs.
    /// This service still re-checks its own direct inputs (null/type/id lookups) since
    /// those aren't boundary-geometry concerns.
    ///
    /// V005 addition: the "out ModelCurveArray footPrintToModelCurvesMapping" parameter
    /// from NewFootPrintRoof was previously declared and discarded. It is now used —
    /// every footprint edge is explicitly marked DefinesSlope = true with
    /// SlopeAngle = 0.0 (flat roof, edges are explicit slope-defining lines rather than
    /// left at Revit's per-roof-type default).
    ///
    /// V005 addition: fallback geometry. If NewFootPrintRoof fails on the real room
    /// boundary (e.g. the "Value cannot be null" failures seen with certain curve
    /// sets), this service retries ONCE with a fixed 4m x 4m square placeholder loop
    /// at host origin (0,0), same level/roof type. If the fallback also fails, the
    /// exception is rethrown and the room is skipped as before — no further fallback
    /// is attempted. Callers (the handler) must check RoofCreationResult.UsedFallback
    /// and log/tally it as a FAILURE (not a success) since the real room geometry was
    /// not used — this is a visible placeholder, not a valid roof for that room.
    /// </summary>
    public static class RoofCreationService
    {
        /// <summary>4 meters, converted to feet (Revit's internal unit) — fixed fallback square side length.</summary>
        private const double FallbackSquareSideFeet = 4.0 / 0.3048;

        public static RoofCreationResult Create(Document doc, CurveLoop outerLoop, ElementId roofTypeId, Level level)
        {
            var diag = new List<string>();
            void Log(string msg) => diag.Add(msg);

            Log($"Create() called. Level='{level?.Name}' (Id={level?.Id?.Value}), " +
                $"RoofTypeId={roofTypeId?.Value}, outerLoop null={outerLoop == null}.");

            // Validate direct inputs (same as before), each check logged individually.
            if (outerLoop == null)
            {
                Log("FAIL: outerLoop is null.");
                throw new ArgumentNullException(nameof(outerLoop), "Outer boundary loop is null.");
            }
            if (!outerLoop.Any())
            {
                Log("FAIL: outerLoop has zero curves.");
                throw new ArgumentException("Outer boundary loop is empty.", nameof(outerLoop));
            }
            Log($"OK: outerLoop has {outerLoop.Count()} curve(s).");

            int idx = 0;
            foreach (var c in outerLoop)
            {
                var p0 = c.GetEndPoint(0);
                var p1 = c.GetEndPoint(1);
                Log($"  Curve[{idx}] type={c.GetType().Name} " +
                    $"start=({p0.X:F4},{p0.Y:F4},{p0.Z:F4}) end=({p1.X:F4},{p1.Y:F4},{p1.Z:F4}) " +
                    $"length={c.Length:F4}ft.");
                idx++;
            }

            bool isPlanar;
            try
            {
                var plane = outerLoop.GetPlane();
                isPlanar = true;
                Log($"OK: outerLoop is planar. Plane origin Z={plane.Origin.Z:F4}, normal=({plane.Normal.X:F4},{plane.Normal.Y:F4},{plane.Normal.Z:F4}).");
            }
            catch (Exception planeEx)
            {
                isPlanar = false;
                Log($"WARN: outerLoop.GetPlane() threw: {planeEx.GetType().Name}: {planeEx.Message}. Loop may be non-planar.");
            }

            if (level == null)
            {
                Log("FAIL: level is null.");
                throw new ArgumentNullException(nameof(level), "Target level is null.");
            }
            if (doc.GetElement(level.Id) == null)
            {
                Log($"FAIL: Level Id {level.Id.Value} not found in document.");
                throw new ArgumentException($"Level ID {level.Id.Value} not found in document.", nameof(level));
            }
            Log($"OK: Level '{level.Name}' (Id={level.Id.Value}) resolved. Elevation={level.Elevation:F4}ft.");

            if (roofTypeId == null || roofTypeId == ElementId.InvalidElementId)
            {
                Log($"FAIL: roofTypeId is null or InvalidElementId (value={roofTypeId?.Value}).");
                throw new ArgumentException($"Invalid roofTypeId: {roofTypeId?.Value}", nameof(roofTypeId));
            }
            var roofType = doc.GetElement(roofTypeId) as RoofType;
            if (roofType == null)
            {
                Log($"FAIL: RoofType with ID {roofTypeId.Value} not found or element is not a RoofType.");
                throw new ArgumentException($"RoofType with ID {roofTypeId.Value} not found or not a RoofType.", nameof(roofTypeId));
            }
            Log($"OK: RoofType '{roofType.Name}' (Id={roofType.Id.Value}) resolved.");

            try
            {
                var compoundStructure = roofType.GetCompoundStructure();
                Log(compoundStructure != null
                    ? $"OK: RoofType '{roofType.Name}' has a compound structure with {compoundStructure.GetLayers().Count} layer(s)."
                    : $"WARN: RoofType '{roofType.Name}' has NULL compound structure — this is a known cause of NewFootPrintRoof failures.");
            }
            catch (Exception csEx)
            {
                Log($"WARN: roofType.GetCompoundStructure() threw: {csEx.GetType().Name}: {csEx.Message}.");
            }

            // Attempt 1: the real room boundary.
            Log("Attempt 1: creating roof from real room boundary curve...");
            try
            {
                var roof = CreateFootPrintRoofInternal(doc, outerLoop, roofType, level, Log);
                Log("Attempt 1 SUCCEEDED.");
                return new RoofCreationResult { Roof = roof, UsedFallback = false, Diagnostics = diag };
            }
            catch (Exception firstEx)
            {
                Log($"Attempt 1 FAILED: {firstEx.GetType().FullName}: {firstEx.Message}");
                Log($"Attempt 1 stack trace: {firstEx.StackTrace}");
                if (firstEx.InnerException != null)
                    Log($"Attempt 1 inner exception: {firstEx.InnerException.GetType().FullName}: {firstEx.InnerException.Message}");

                string originalFailureReason =
                    $"Roof creation failed for level '{level.Name}' with roof type '{roofType.Name}'. " +
                    $"Curve count: {outerLoop.Count()}. Inner: {firstEx.Message}";

                // Attempt 2: fixed 4m x 4m fallback square at host origin (0,0), same level/type.
                Log("Attempt 2: building 4m x 4m fallback square at host origin (0,0)...");
                CurveLoop fallbackLoop;
                try
                {
                    fallbackLoop = BuildFallbackSquareLoop();
                    Log($"OK: fallback square built. Side={FallbackSquareSideFeet:F4}ft.");
                }
                catch (Exception buildEx)
                {
                    Log($"FAIL: fallback geometry construction threw: {buildEx.GetType().FullName}: {buildEx.Message}");
                    throw new InvalidOperationException(
                        originalFailureReason + $" Fallback geometry could not be built: {buildEx.Message}",
                        firstEx);
                }

                Log("Attempt 2: creating roof from fallback square...");
                try
                {
                    var fallbackRoof = CreateFootPrintRoofInternal(doc, fallbackLoop, roofType, level, Log);
                    Log("Attempt 2 SUCCEEDED (fallback roof created).");
                    return new RoofCreationResult
                    {
                        Roof = fallbackRoof,
                        UsedFallback = true,
                        OriginalFailureReason = originalFailureReason,
                        Diagnostics = diag
                    };
                }
                catch (Exception fallbackEx)
                {
                    Log($"Attempt 2 FAILED: {fallbackEx.GetType().FullName}: {fallbackEx.Message}");
                    Log($"Attempt 2 stack trace: {fallbackEx.StackTrace}");
                    if (fallbackEx.InnerException != null)
                        Log($"Attempt 2 inner exception: {fallbackEx.InnerException.GetType().FullName}: {fallbackEx.InnerException.Message}");

                    // Fallback also failed — stop here, no further fallback attempted.
                    var finalEx = new InvalidOperationException(
                        originalFailureReason + $" Fallback roof (4m x 4m at origin) also failed: {fallbackEx.Message}",
                        fallbackEx);
                    finalEx.Data["Diagnostics"] = diag;
                    throw finalEx;
                }
            }
        }

        /// <summary>
        /// Builds a fixed 4m x 4m square CurveLoop at host origin (0,0), used as the
        /// fallback footprint when the real room boundary fails roof creation.
        ///
        /// IMPORTANT: this is a flat 2D profile — all points use Z = 0. Do NOT set Z to
        /// level.Elevation or any other absolute height here. NewFootPrintRoof positions
        /// the roof using the separate Level parameter; the footprint curve only needs
        /// to be planar (Z constant across all points). Adding level.Elevation to the
        /// curve's Z would double the vertical offset — once from the curve, once from
        /// the Level argument — and was deliberately avoided.
        /// </summary>
        private static CurveLoop BuildFallbackSquareLoop()
        {
            const double z = 0.0;
            var p0 = new XYZ(0, 0, z);
            var p1 = new XYZ(FallbackSquareSideFeet, 0, z);
            var p2 = new XYZ(FallbackSquareSideFeet, FallbackSquareSideFeet, z);
            var p3 = new XYZ(0, FallbackSquareSideFeet, z);

            var loop = new CurveLoop();
            loop.Append(Line.CreateBound(p0, p1));
            loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3));
            loop.Append(Line.CreateBound(p3, p0));
            return loop;
        }

        /// <summary>
        /// Shared creation + slope-setting logic used by both the real-curve attempt
        /// and the fallback-square attempt. Throws on failure — callers decide what to
        /// do next (fallback vs. rethrow). Every step is logged via the Log callback.
        /// </summary>
        private static FootPrintRoof CreateFootPrintRoofInternal(Document doc, CurveLoop loop, RoofType roofType, Level level, Action<string> Log)
        {
            var curveArray = new CurveArray();
            int n = 0;
            foreach (var curve in loop)
            {
                curveArray.Append(curve);
                n++;
            }
            Log($"  CreateFootPrintRoofInternal: built CurveArray with {n} curve(s) from input loop.");

            Log($"  Calling doc.Create.NewFootPrintRoof(curveArray, level='{level.Name}', roofType='{roofType.Name}')...");
            ModelCurveArray modelCurves;
            FootPrintRoof roof;
            try
            {
                roof = doc.Create.NewFootPrintRoof(curveArray, level, roofType, out modelCurves);
            }
            catch (Exception apiEx)
            {
                Log($"  NewFootPrintRoof THREW: {apiEx.GetType().FullName}: {apiEx.Message}");
                throw;
            }

            if (roof == null)
            {
                Log("  NewFootPrintRoof returned NULL (no exception thrown, but result is null).");
                throw new InvalidOperationException(
                    $"NewFootPrintRoof returned null for level '{level.Name}' – geometry likely invalid.");
            }
            Log($"  NewFootPrintRoof SUCCEEDED. New roof Id={roof.Id.Value}.");

            // footPrintToModelCurvesMapping (modelCurves) is documented as "An array of
            // Model Curves corresponding to the set of Curves input in the footPrint" —
            // i.e. modelCurves[i] corresponds to curveArray[i], in the same order. We
            // walk both in parallel by index (rather than iterating modelCurves alone)
            // so that correspondence is explicit and available for any future per-edge
            // logic, even though every edge currently gets the same flat (0°) slope.
            var inputCurves = new List<Curve>();
            var inputIterator = curveArray.ForwardIterator();
            inputIterator.Reset();
            while (inputIterator.MoveNext())
            {
                if (inputIterator.Current is Curve inputCurve)
                    inputCurves.Add(inputCurve);
            }

            var outputModelCurves = new List<ModelCurve>();
            var outputIterator = modelCurves.ForwardIterator();
            outputIterator.Reset();
            while (outputIterator.MoveNext())
            {
                if (outputIterator.Current is ModelCurve modelCurve)
                    outputModelCurves.Add(modelCurve);
            }

            Log($"  modelCurves mapping: {inputCurves.Count} input curve(s), {outputModelCurves.Count} output ModelCurve(s).");
            if (inputCurves.Count != outputModelCurves.Count)
                Log($"  WARN: input/output curve counts do not match — mapping may be incomplete.");

            int pairCount = Math.Min(inputCurves.Count, outputModelCurves.Count);
            for (int i = 0; i < pairCount; i++)
            {
                // inputCurves[i] is the footprint curve originally passed in;
                // outputModelCurves[i] is its corresponding ModelCurve, per the API's
                // documented 1:1 mapping. Every edge is set flat (0°) here.
                var correspondingModelCurve = outputModelCurves[i];
                try
                {
                    roof.set_DefinesSlope(correspondingModelCurve, true);
                    roof.set_SlopeAngle(correspondingModelCurve, 0.0);
                }
                catch (Exception slopeEx)
                {
                    Log($"  WARN: setting slope on edge[{i}] threw: {slopeEx.GetType().Name}: {slopeEx.Message} (roof was still created).");
                }
            }
            Log($"  Slope set to 0.0 on {pairCount} edge(s).");

            return roof;
        }
    }

    /// <summary>
    /// Result of RoofCreationService.Create. UsedFallback = true means the real room
    /// boundary failed and a fixed 4m x 4m placeholder square at host origin was created
    /// instead — the handler MUST log this as a failure/warning (with
    /// OriginalFailureReason), not a normal success, since the room's actual geometry
    /// was not used.
    ///
    /// Diagnostics contains a full, ordered trace of every check, input value, and
    /// outcome recorded during Create() — always populated, on both success and
    /// failure paths — so the handler can log it in full to the UI log (Info level)
    /// for root-cause diagnosis.
    /// </summary>
    public class RoofCreationResult
    {
        public FootPrintRoof Roof { get; set; }
        public bool UsedFallback { get; set; }
        public string OriginalFailureReason { get; set; }
        public List<string> Diagnostics { get; set; } = new();
    }
}
