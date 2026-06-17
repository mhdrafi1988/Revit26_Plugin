namespace Revit26_Plugin.AutoLiner_V01.Helpers
{
    public static class GeometryTolerance
    {
        public static double MmToFt(double mm) => mm / 304.8;

        public const double Z_TOL_MM = 5;
        public const double DIST_TOL_MM = 100;
        public const double MERGE_MM = 300;
        public const double MIN_EDGE_MM = 100;
    }
}
