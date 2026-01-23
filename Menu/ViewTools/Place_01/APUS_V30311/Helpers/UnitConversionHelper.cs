using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V311.Helpers
{
    public static class UnitConversionHelper
    {
        public static double MmToFeet(double mm)
            => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
