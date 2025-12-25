using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_A101.Helpers
{
    internal static class ProjectionHelper
    {
        /// <summary>
        /// Projects a 3D point onto a given plane.
        /// </summary>
        public static XYZ ProjectToPlane(XYZ point, Plane plane)
        {
            XYZ v = point - plane.Origin;
            double d = v.DotProduct(plane.Normal);
            return point - d * plane.Normal;
        }
    }
}
