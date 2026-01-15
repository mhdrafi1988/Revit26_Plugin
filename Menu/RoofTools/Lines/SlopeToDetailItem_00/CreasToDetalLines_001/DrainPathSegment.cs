using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_adv_V001.Models
{
    /// <summary>
    /// Represents one drainage path segment with both 3D logic and 2D geometry.
    /// </summary>
    public class DrainPathSegment
    {
        public XYZ Start3D { get; set; }
        public XYZ End3D { get; set; }

        public XYZ Start2D { get; set; }
        public XYZ End2D { get; set; }

        public double DeltaZ => Start3D.Z - End3D.Z;

        public DetailCurve DetailCurve { get; set; }
    }
}
