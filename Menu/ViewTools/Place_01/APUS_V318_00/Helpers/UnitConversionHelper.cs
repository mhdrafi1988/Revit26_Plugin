// File: UnitConversionHelper.cs
using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V318.Helpers
{
    public static class UnitConversionHelper
    {
        // Conversion factors
        private const double MillimetersPerFoot = 304.8;
        private const double InchesPerFoot = 12.0;

        /// <summary>
        /// Converts millimeters to feet
        /// </summary>
        public static double MmToFeet(double mm)
        {
            return mm / MillimetersPerFoot;
        }

        /// <summary>
        /// Converts feet to millimeters
        /// </summary>
        public static double FeetToMm(double feet)
        {
            return feet * MillimetersPerFoot;
        }

        /// <summary>
        /// Converts inches to feet
        /// </summary>
        public static double InchesToFeet(double inches)
        {
            return inches / InchesPerFoot;
        }

        /// <summary>
        /// Converts feet to inches
        /// </summary>
        public static double FeetToInches(double feet)
        {
            return feet * InchesPerFoot;
        }

        /// <summary>
        /// Converts Revit internal units (feet) to millimeters
        /// </summary>
        public static double InternalUnitsToMm(double internalUnits)
        {
            return FeetToMm(internalUnits);
        }

        /// <summary>
        /// Converts millimeters to Revit internal units (feet)
        /// </summary>
        public static double MmToInternalUnits(double mm)
        {
            return MmToFeet(mm);
        }

        /// <summary>
        /// Converts points (1 point = 1/72 inch) to feet
        /// </summary>
        public static double PointsToFeet(double points)
        {
            return InchesToFeet(points / 72.0);
        }

        /// <summary>
        /// Converts feet to points
        /// </summary>
        public static double FeetToPoints(double feet)
        {
            return FeetToInches(feet) * 72.0;
        }

        /// <summary>
        /// Formats a length in feet to readable string
        /// </summary>
        public static string FormatFeet(double feet, bool includeInches = true)
        {
            if (includeInches)
            {
                int wholeFeet = (int)feet;
                double remainingFeet = feet - wholeFeet;
                int inches = (int)(remainingFeet * 12);
                return $"{wholeFeet}' {inches}\"";
            }
            return $"{feet:F2}'";
        }

        /// <summary>
        /// Formats a length in millimeters to readable string
        /// </summary>
        public static string FormatMm(double mm, bool includeFeet = true)
        {
            if (includeFeet && mm > MillimetersPerFoot)
            {
                double feet = MmToFeet(mm);
                return FormatFeet(feet);
            }
            return $"{mm:F0} mm";
        }

        /// <summary>
        /// Converts area from square feet to square millimeters
        /// </summary>
        public static double SquareFeetToSquareMm(double squareFeet)
        {
            return squareFeet * MillimetersPerFoot * MillimetersPerFoot;
        }

        /// <summary>
        /// Converts area from square millimeters to square feet
        /// </summary>
        public static double SquareMmToSquareFeet(double squareMm)
        {
            return squareMm / (MillimetersPerFoot * MillimetersPerFoot);
        }
    }
}