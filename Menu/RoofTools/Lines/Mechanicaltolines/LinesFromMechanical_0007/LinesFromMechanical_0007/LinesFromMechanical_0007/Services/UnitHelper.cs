namespace Revit26_Plugin.LinesFromMechanical.V007.Services;

public static class UnitHelper
{
    /// <summary>
    /// Shared spatial tolerance used for rounded-point deduplication keys.
    /// 0.001 ft ≈ 0.3 mm — prevents merging of distinct nearby points.
    /// </summary>
    public const double RoundingToleranceFt = 0.001;

    public static double MillimetersToFeet(double mm) => mm / 304.8;
    public static double FeetToMillimeters(double feet) => feet * 304.8;
}
