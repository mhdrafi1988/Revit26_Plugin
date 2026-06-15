// File: Helpers/UnitConversionHelper.cs
using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V330.Helpers
{
    public static class UnitConversionHelper
    {
        private const double MillimetersPerFoot = 304.8;
        private const double InchesPerFoot      = 12.0;

        public static double MmToFeet(double mm)       => mm / MillimetersPerFoot;
        public static double FeetToMm(double feet)     => feet * MillimetersPerFoot;
        public static double InchesToFeet(double in_)  => in_ / InchesPerFoot;
        public static double FeetToInches(double feet) => feet * InchesPerFoot;

        public static double InternalUnitsToMm(double internalUnits) => FeetToMm(internalUnits);
        public static double MmToInternalUnits(double mm)            => MmToFeet(mm);

        public static double PointsToFeet(double points) => InchesToFeet(points / 72.0);
        public static double FeetToPoints(double feet)   => FeetToInches(feet) * 72.0;

        public static string FormatFeet(double feet, bool includeInches = true)
        {
            if (includeInches)
            {
                int    wholeFeet      = (int)feet;
                double remainingFeet  = feet - wholeFeet;
                int    inches         = (int)(remainingFeet * 12);
                return $"{wholeFeet}' {inches}\"";
            }
            return $"{feet:F2}'";
        }

        public static string FormatMm(double mm, bool includeFeet = true)
        {
            if (includeFeet && mm > MillimetersPerFoot)
                return FormatFeet(MmToFeet(mm));
            return $"{mm:F0} mm";
        }

        public static double SquareFeetToSquareMm(double squareFeet)
            => squareFeet * MillimetersPerFoot * MillimetersPerFoot;

        public static double SquareMmToSquareFeet(double squareMm)
            => squareMm / (MillimetersPerFoot * MillimetersPerFoot);
    }
}
