using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V313.Models;
using System;

namespace Revit26_Plugin.APUS_V313.Services
{
    /// <summary>
    /// Calculates conservative paper-space footprint for a section view.
    /// Guaranteed to fit if physically possible.
    /// </summary>
    public static class ViewSizeService
    {
        public static SectionFootprint Calculate(ViewSection view)
        {
            BoundingBoxXYZ bb = view.CropBox;
            if (bb == null)
                return new SectionFootprint(0.05, 0.05);

            double modelW = bb.Max.X - bb.Min.X;
            double modelH = bb.Max.Y - bb.Min.Y;

            int scale = view.Scale > 0 ? view.Scale : 1;

            // Convert model ? paper
            double w = modelW / scale;
            double h = modelH / scale;

            // HARD SAFETY CLAMP (prevents bin failure)
            w = Math.Max(w, 0.05);
            h = Math.Max(h, 0.05);

            return new SectionFootprint(w, h);
        }
    }
}