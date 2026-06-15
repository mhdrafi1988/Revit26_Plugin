using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V315.Models.Entities;

namespace Revit26_Plugin.APUS_V315.Services.Abstractions;

public interface IViewSizeCalculator
{
    SectionFootprint Calculate(ViewSection view);
    double GetScale(ViewSection view);
}