// File: UnitConversionHelper.cs
using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V314.Helpers
{
    public static class UnitConversionHelper
    {
        public static double MmToFeet(double mm)
            => UnitUtils.Convert(mm, UnitTypeId.Millimeters, UnitTypeId.Feet);
        // For older Revit API, you might need: 
        // => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

        public static double FeetToMm(double feet)
            => UnitUtils.Convert(feet, UnitTypeId.Feet, UnitTypeId.Millimeters);
        // For older Revit API:
        // => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);

        public static double InchesToFeet(double inches)
            => inches / 12.0;

        public static double FeetToInches(double feet)
            => feet * 12.0;
    }
}