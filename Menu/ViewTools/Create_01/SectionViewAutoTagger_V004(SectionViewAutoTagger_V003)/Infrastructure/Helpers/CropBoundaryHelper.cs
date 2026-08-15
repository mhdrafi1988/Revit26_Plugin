using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V004
{
    /// <summary>
    /// Resolves the vertical alignment line (view-space X) that tag heads
    /// are placed along, based on the view's crop boundary and the global
    /// AlignmentSide/OffsetMm settings.
    ///
    /// ASSUMPTION (flagged as highest-risk geometry in this tool): uses
    /// view.CropBox when CropBoxActive is true. If crop is OFF, there is no
    /// reliable "view boundary" to offset from in the Revit API — this
    /// implementation falls back to the view's CropBox extents anyway (Revit
    /// always maintains a CropBox even when inactive/hidden), but the result
    /// may not visually match what the user sees on the sheet if crop
    /// region differs from the visible/annotation crop. Needs verification
    /// against a real project during testing.
    /// </summary>
    public class CropBoundaryHelper
    {
        private const double MmToFeet = 1.0 / 304.8;

        /// <summary>
        /// Returns the alignment line's X coordinate in the view's own
        /// coordinate space (crop box space — same space as element
        /// locations projected via view.CropBox.Transform).
        /// </summary>
        public double GetAlignmentLineX(View view, AlignmentSide side, double offsetMm)
        {
            BoundingBoxXYZ cropBox = view.CropBox;
            double offsetFeet = offsetMm * MmToFeet;

            return side == AlignmentSide.Left
                ? cropBox.Min.X + offsetFeet
                : cropBox.Max.X - offsetFeet;
        }
    }
}
