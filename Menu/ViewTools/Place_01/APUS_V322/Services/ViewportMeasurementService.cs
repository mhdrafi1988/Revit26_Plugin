// File: ViewportMeasurementService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS.V322.Models;

namespace Revit26_Plugin.APUS.V322.Services
{
    /// <summary>
    /// Replaces V320's ViewSizeService. V320 computed footprint from
    /// CropBox/scale math, while a separate part of the pipeline used
    /// Viewport.GetBoxOutline() for actual packing — the two disagreed,
    /// making the reported utilization % inconsistent with the real layout.
    ///
    /// V321 uses GetBoxOutline() everywhere: it reflects the real rendered
    /// viewport (crop boundary, title/tag text, revision clouds, etc.),
    /// which is only obtainable once the viewport actually exists on a
    /// sheet. Callers must create a temporary viewport (Pass 1 in
    /// EvenGapPlacementService) before calling Measure.
    /// </summary>
    public static class ViewportMeasurementService
    {
        public static SectionFootprint Measure(Viewport viewport)
        {
            if (viewport == null)
                return new SectionFootprint(0.05, 0.05);

            var outline = viewport.GetBoxOutline();
            double width = outline.MaximumPoint.X - outline.MinimumPoint.X;
            double height = outline.MaximumPoint.Y - outline.MinimumPoint.Y;

            // Hard safety clamp — mirrors the V320 ViewSizeService floor so
            // degenerate outlines never produce a zero/negative footprint.
            width = System.Math.Max(width, 0.05);
            height = System.Math.Max(height, 0.05);

            return new SectionFootprint(width, height);
        }
    }
}
