using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Models.Entities;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using System;

namespace Revit26_Plugin.APUS_V315.Services.Implementation;

public sealed class ViewSizeCalculator : IViewSizeCalculator
{
    public SectionFootprint Calculate(ViewSection view)
    {
        if (view == null || !view.IsValidObject)
            return new SectionFootprint(0.05, 0.05);

        var cropBox = view.CropBox;
        if (cropBox == null)
            return new SectionFootprint(0.05, 0.05);

        double modelWidth = cropBox.Max.X - cropBox.Min.X;
        double modelHeight = cropBox.Max.Y - cropBox.Min.Y;

        int scale = GetScale(view);
        if (scale <= 0) scale = 100;

        // Convert model space to paper space
        double width = modelWidth / scale;
        double height = modelHeight / scale;

        return new SectionFootprint(width, height);
    }

    public double GetScale(ViewSection view)
    {
        if (view == null || !view.IsValidObject)
            return 100;

        var param = view.get_Parameter(BuiltInParameter.VIEW_SCALE);
        return param?.AsInteger() ?? 100;
    }
}