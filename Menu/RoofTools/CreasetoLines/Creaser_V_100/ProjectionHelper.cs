using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser.Helpers
{
    public static class ProjectionHelper
    {
        /// <summary>
        /// Projects a 3D point onto the active view plane (2D-safe).
        /// Works for Plan / Section / Drafting views.
        /// </summary>
        public static XYZ ProjectToViewPlane(XYZ point, View view)
        {
            if (point == null)
                throw new ArgumentNullException(nameof(point));

            if (view == null)
                throw new ArgumentNullException(nameof(view));

            Plane plane = Plane.CreateByNormalAndOrigin(
                view.ViewDirection,
                view.Origin
            );

            XYZ v = point - plane.Origin;
            double distance = v.DotProduct(plane.Normal);

            return point - distance * plane.Normal;
        }
    }
}
