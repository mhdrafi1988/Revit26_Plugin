using System;

namespace Revit26_Plugin.APUS_V315.Helpers;

public static class UnitConversionHelper
{
    private const double MillimetersPerFoot = 304.8;
    private const double InchesPerFoot = 12.0;

    public static double MmToFeet(double mm) => mm / MillimetersPerFoot;
    public static double FeetToMm(double feet) => feet * MillimetersPerFoot;
    public static double InchesToFeet(double inches) => inches / InchesPerFoot;
    public static double FeetToInches(double feet) => feet * InchesPerFoot;
    public static double MmToInternalUnits(double mm) => MmToFeet(mm);
    public static double InternalUnitsToMm(double feet) => FeetToMm(feet);

    public static string FormatFeet(double feet, bool includeInches = true)
    {
        if (!includeInches)
            return $"{feet:F2}'";

        int wholeFeet = (int)feet;
        double remainingFeet = feet - wholeFeet;
        int inches = (int)Math.Round(remainingFeet * 12);

        if (inches == 12)
        {
            wholeFeet++;
            inches = 0;
        }

        return inches > 0
            ? $"{wholeFeet}' {inches}\""
            : $"{wholeFeet}'";
    }

    public static string FormatMm(double mm, bool includeFeet = true)
    {
        if (includeFeet && mm > MillimetersPerFoot)
            return FormatFeet(MmToFeet(mm));

        return $"{mm:F0} mm";
    }
}