using Autodesk.Revit.DB;

namespace Revit26_Plugin.UnitConverter
{
    /// <summary>
    /// Centralized unit conversion helper.
    /// Use this everywhere. Do NOT duplicate this class.
    /// </summary>
    public static class UnitHelper
    {
        // ---------- LENGTH ----------

        public static double MmToFeet(double mm)
        {
            return UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        }

        public static double CmToFeet(double cm)
        {
            return UnitUtils.ConvertToInternalUnits(cm, UnitTypeId.Centimeters);
        }

        public static double MToFeet(double meters)
        {
            return UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);
        }

        public static double FeetToMm(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
        }

        // ---------- AREA ----------

        public static double SqmToSqft(double sqm)
        {
            return UnitUtils.ConvertToInternalUnits(sqm, UnitTypeId.SquareMeters);
        }

        public static double SqftToSqm(double sqft)
        {
            return UnitUtils.ConvertFromInternalUnits(sqft, UnitTypeId.SquareMeters);
        }

        // ---------- ANGLE ----------

        public static double DegreesToRadians(double degrees)
        {
            return degrees * (System.Math.PI / 180.0);
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / System.Math.PI);
        }
    }
}
