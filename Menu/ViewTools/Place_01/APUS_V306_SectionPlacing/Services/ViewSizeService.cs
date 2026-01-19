using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V306.Models;
using System;

namespace Revit26_Plugin.APUS_V306.Services
{
    public static class ViewSizeService
    {
        public static SectionFootprint Calculate(ViewSection view)
        {
            BoundingBoxXYZ bb = view.CropBox;

            if (bb == null)
                return new SectionFootprint(1.0, 1.0);

            double modelW = bb.Max.X - bb.Min.X;
            double modelH = bb.Max.Y - bb.Min.Y;

            int scale = view.Scale > 0 ? view.Scale : 1;

            double w = modelW / scale;
            double h = modelH / scale;

            w = Math.Max(w, 1.0);
            h = Math.Max(h, 1.0);

            return new SectionFootprint(w, h);
        }
    }
}