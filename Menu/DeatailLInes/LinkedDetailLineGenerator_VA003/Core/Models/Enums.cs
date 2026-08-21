namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models
{
    /// <summary>
    /// Broad classification of how a selected linked-model Type will be represented
    /// as 2D Detail Line geometry. Drives which extraction pipeline (Profile / Linear /
    /// Point) is used for a given ElementMapping.
    /// </summary>
    public enum RepresentationGroup
    {
        Profile,
        Linear,
        Point
    }

    /// <summary>
    /// Specific representation mode within a RepresentationGroup.
    /// Profile → Boundary. Linear → Centerline. Point → Circle / Rectangle.
    /// </summary>
    public enum RepresentationMode
    {
        Boundary,
        Centerline,
        Circle,
        Rectangle
    }

    /// <summary>
    /// Point marker shape used for Point-group representations.
    /// Circle and Rectangle each have independent size settings (PointMarkerSettings).
    /// </summary>
    public enum PointMarkerShape
    {
        Circle,
        Rectangle
    }

    /// <summary>
    /// How a Rectangle point marker is rotated around its center point.
    /// Circle markers are rotationally symmetric and ignore this entirely.
    /// See PointProcessingEngine.ComputeRectangleRotationRadians for how each
    /// mode resolves to an actual angle at generation time.
    /// </summary>
    public enum RectangleAlignmentMode
    {
        /// <summary>Matches the linked FamilyInstance's own placement rotation
        /// (converted into host coordinates via the link's transform). Falls back
        /// to ProjectAxes, logged, if the element isn't a point-placed FamilyInstance.</summary>
        InstanceRotation,

        /// <summary>Axis-aligned to the host model's project X/Y — the original,
        /// unrotated behavior.</summary>
        ProjectAxes,

        /// <summary>Rotated to align with True North rather than Project North.</summary>
        TrueNorth,

        /// <summary>Rotated to match the active view's Right/Up directions —
        /// matters mainly for rotated crop regions or non-plan views.</summary>
        ViewAxes,

        /// <summary>Fixed angle entered by the user (ManualAngleDegrees), for cases
        /// none of the automatic modes produce the desired result.</summary>
        Manual,

        /// <summary>Derives rotation from the linked element's own footprint geometry
        /// rather than its placement rotation or any project/view axis:
        ///   1. Outer Profile Detection — find the longest straight (Line) segment(s)
        ///      in the element's outer boundary loop; if the outer loop has no straight
        ///      segments (e.g. a circular footprint), fall back to the longest side of
        ///      its bounding box.
        ///   2. Axis Calculation — take that reference segment's direction as the
        ///      primary axis.
        ///   3. Inner Loop Adjustment — every inner loop aligns parallel to that same
        ///      axis (referencing each inner loop's own longest side). For this engine,
        ///      which renders exactly one rectangle per point, this step has no separate
        ///      effect — the single axis from step 2 already IS the marker's rotation.
        /// See PointProcessingEngine.ComputeOuterProfileAxisRotationRadians.</summary>
        OuterProfileAxis
    }

    /// <summary>
    /// Fallback shape used to replace non-analytic (spline/NURBS) curves when
    /// "Replace complex curves with simplified shape" is enabled in Complex Curve Handling.
    /// StraightChord: single Line from curve start to end.
    /// BestFitArc: single Arc fit through start/mid/end; falls back to StraightChord
    /// automatically if curvature sign is inconsistent (see GeometryExtractionService).
    /// </summary>
    public enum SplineFallbackShape
    {
        StraightChord,
        BestFitArc
    }

    /// <summary>
    /// Profile-group (Floor/Roof) boundary extraction method, selectable per
    /// mapping row in the Mapping Grid. SolidGeometry (the original/default method)
    /// reads the element's solid geometry and picks the topmost horizontal face's
    /// EdgeLoops. ProfileBased reads the element's own sketch/profile curves
    /// (Floor/RoofBase.GetSketch()) instead — closer to the original design intent
    /// for sketch-based elements, but unavailable for non-sketch-based ones (falls
    /// back to SolidGeometry automatically, logged, per Rafi's explicit choice).
    /// Meaningless for Linear/Point mappings (Wall/Beam use LocationCurve directly;
    /// Point markers aren't sketch-based) — left at its default there, no effect.
    /// </summary>
    public enum ProfileExtractionMethod
    {
        SolidGeometry,
        ProfileBased
    }

    /// <summary>
    /// Overall tool run state. Drives the "gray out / inactive" UI behavior:
    /// form is interactive only while Idle or Configuring; becomes read-only
    /// once Complete, until the user explicitly resets to a new session.
    /// </summary>
    public enum ToolRunState
    {
        Idle,
        Configuring,
        Running,
        Complete
    }
}
