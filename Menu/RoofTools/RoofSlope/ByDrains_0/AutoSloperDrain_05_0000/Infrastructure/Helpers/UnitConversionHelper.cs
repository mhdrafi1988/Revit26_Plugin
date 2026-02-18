using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoSlope.V5_00.Infrastructure.Helpers
{
    public static class UnitConversionHelper
    {
        public const double FeetToMm = 304.8;
        public const double MmToFeet = 1.0 / 304.8;
        public const double FeetToMeters = 0.3048;
        public const double MetersToFeet = 3.28084;

        public static double ToMillimeters(double feet)
        {
            return feet * FeetToMm;
        }

        public static double ToFeet(double millimeters)
        {
            return millimeters * MmToFeet;
        }

        public static double ToMeters(double feet)
        {
            return feet * FeetToMeters;
        }

        public static double ToFeetFromMeters(double meters)
        {
            return meters * MetersToFeet;
        }

        public static double ConvertInternalToMm(double internalUnits)
        {
            return UnitUtils.ConvertFromInternalUnits(internalUnits, UnitTypeId.Millimeters);
        }

        public static double ConvertMmToInternal(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
        }

        public static double ConvertInternalToMeters(double internalUnits)
        {
            return UnitUtils.ConvertFromInternalUnits(internalUnits, UnitTypeId.Meters);
        }

        public static double ConvertMetersToInternal(double meters)
        {
            return UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);
        }
    }
}