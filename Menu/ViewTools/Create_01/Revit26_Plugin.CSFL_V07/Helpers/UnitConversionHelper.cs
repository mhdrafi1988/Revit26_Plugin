using Autodesk.Revit.DB;

namespace Revit26_Plugin.CSFL_V07.Helpers
{
    /// <summary>
    /// Centralized unit conversion helper for Revit 2026.
    /// Prevents magic UnitUtils calls scattered across code.
    /// </summary>
    public static class UnitConversionHelper
    {
        public static double MmToFt(double mm)
        {
            return UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        }

        public static double FtToMm(double ft)
        {
            return UnitUtils.ConvertFromInternalUnits(ft, UnitTypeId.Millimeters);
        }
    }
}
