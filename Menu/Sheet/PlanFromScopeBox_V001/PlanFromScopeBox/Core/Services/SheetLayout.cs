namespace Revit26_Plugin.PlanFromScopeBox.V001.Core.Services
{
    /// <summary>
    /// Single source of truth for the usable placement area on a sheet, in millimetres.
    /// Used by BOTH the ViewModel (grid "Fits" preview) and the engine (actual packing), so
    /// the preview and the real result can never diverge.
    ///
    /// Sheet size is a PARAMETER (fed from the measured title block), defaulting to ISO A0.
    /// Usable area = sheet minus symmetric page margins minus the title-block clear zone
    /// (manual right/bottom strips).
    /// </summary>
    public static class SheetLayout
    {
        // ISO A0 landscape, in mm — defaults when no measurement is available.
        public const double A0WidthMm = 1189.0;
        public const double A0HeightMm = 841.0;

        /// <summary>
        /// Computes usable width/height (mm) for packing on a sheet of the given size.
        /// </summary>
        public static (double wMm, double hMm) Usable(
            double sheetWmm, double sheetHmm,
            double marginMm, double tbClearRightMm, double tbClearBottomMm)
        {
            if (sheetWmm <= 0) sheetWmm = A0WidthMm;
            if (sheetHmm <= 0) sheetHmm = A0HeightMm;

            double w = sheetWmm - 2 * marginMm - tbClearRightMm;
            double h = sheetHmm - 2 * marginMm - tbClearBottomMm;
            if (w < 0) w = 0;
            if (h < 0) h = 0;
            return (w, h);
        }

        /// <summary>
        /// Printed footprint of a view in mm from model extents (metres) and scale denominator
        /// (e.g. 100 for 1:100).
        /// </summary>
        public static (double wMm, double hMm) Footprint(double widthM, double heightM, double scaleDenom)
        {
            if (scaleDenom <= 0) scaleDenom = 100;
            return (widthM * 1000.0 / scaleDenom, heightM * 1000.0 / scaleDenom);
        }

        /// <summary>True if a footprint fits within the given usable area.</summary>
        public static bool Fits(double footprintWmm, double footprintHmm, double usableWmm, double usableHmm)
            => footprintWmm <= usableWmm && footprintHmm <= usableHmm;
    }
}
