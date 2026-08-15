using Autodesk.Revit.DB;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V010.Models
{
    /// <summary>
    /// Immutable snapshot of user-defined creation settings. UI-independent.
    ///
    /// V008: converted from V07's 11-parameter positional constructor to
    /// named `required` init properties — removes transposition risk
    /// (e.g. accidentally swapping TopPaddingMm/BottomOffsetMm at the call site).
    ///
    /// V009: added View Crop properties (CropBoxActive, CropRegionVisible,
    /// AnnotationCropActive, and 4 annotation offset fields) — applied to
    /// each created ViewSection in SectionCreationService.
    /// </summary>
    public class SectionCreationOptions
    {
        public required string Prefix { get; init; }

        public required double FarClipMm { get; init; }
        public required double TopPaddingMm { get; init; }
        public required double BottomPaddingMm { get; init; }
        public required double BottomOffsetMm { get; init; }
        public required double SearchThresholdMm { get; init; }

        /// <summary>Which host categories to search (Floor/Roof x Host/Linked).</summary>
        public required HostSourceSelection HostSource { get; init; }

        /// <summary>
        /// If true and no qualifying host element is found, fall back to
        /// building the section height from SearchThresholdMm around the
        /// line's own elevation instead of skipping the line.
        /// </summary>
        public required bool UseThresholdFallback { get; init; }

        public required ViewFamilyType SectionType { get; init; }

        /// <summary>Optional. Null = no template applied.</summary>
        public View Template { get; init; }

        public required bool OpenAfterCreate { get; init; }
        public required bool DeleteLinesAfterCreate { get; init; }

        public required int ViewScale { get; init; }

        // ================= VIEW CROP (V009 — new) =================

        public required bool CropBoxActive { get; init; }
        public required bool CropRegionVisible { get; init; }
        public required bool AnnotationCropActive { get; init; }

        /// <summary>
        /// Annotation crop offsets in mm, applied via ViewCropRegionShapeManager.
        /// Revit requires these to be non-negative (they expand the annotation
        /// crop outward from the model crop; Revit does not support shrinking
        /// it inward). Only applied when AnnotationCropActive is true.
        /// </summary>
        public required double TopAnnotationOffsetMm { get; init; }
        public required double BottomAnnotationOffsetMm { get; init; }
        public required double LeftAnnotationOffsetMm { get; init; }
        public required double RightAnnotationOffsetMm { get; init; }
    }
}
