using Autodesk.Revit.DB;

namespace Revit26_Plugin.CreaserAdv_V00.Services.Geometry
{
    /// <summary>
    /// Dumb projection helper.
    /// Does NOT change endpoint order.
    /// </summary>
    public static class CreasePlanProjectionHelper
    {
        public static Line ProjectToPlan(Line ordered3dLine, ViewPlan view)
        {
            XYZ a3 = ordered3dLine.GetEndPoint(0); // HIGH
            XYZ b3 = ordered3dLine.GetEndPoint(1); // LOW

            double z = view.GenLevel.Elevation;

            XYZ a2 = new XYZ(a3.X, a3.Y, z);
            XYZ b2 = new XYZ(b3.X, b3.Y, z);

            return Line.CreateBound(a2, b2);
        }
    }
}
