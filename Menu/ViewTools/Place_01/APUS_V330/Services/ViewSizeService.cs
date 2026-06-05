// File: Services/ViewSizeService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V330.Models;
using System;

namespace Revit26_Plugin.APUS_V330.Services
{
    public static class ViewSizeService
    {
        public static SectionFootprint Calculate(ViewSection view)
        {
            BoundingBoxXYZ bb = view.CropBox;
            if (bb == null)
                return new SectionFootprint(0.05, 0.05);

            double modelW = bb.Max.X - bb.Min.X;
            double modelH = bb.Max.Y - bb.Min.Y;
            int    scale  = view.Scale > 0 ? view.Scale : 1;

            double w = Math.Max(modelW / scale, 0.05);
            double h = Math.Max(modelH / scale, 0.05);

            return new SectionFootprint(w, h);
        }
    }
}
