using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinesFromMechanical.V001.Helpers;

public static class UnitHelper
{
    public static double MillimetersToFeet(double millimeters)
    {
        return UnitUtils.ConvertToInternalUnits(
            millimeters,
            UnitTypeId.Millimeters);
    }
}