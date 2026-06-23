using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinesFromMechanical.V003.Services;

public static class UnitHelper
{
    public static double MillimetersToFeet(double millimeters)
    {
        return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
    }

    public static double FeetToMillimeters(double feet)
    {
        return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
    }
}