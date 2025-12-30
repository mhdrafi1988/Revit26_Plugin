using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V32.Models
{
    public class CreasePath
    {
        public XYZ Corner { get; }
        public XYZ Drain { get; }
        public List<Curve> Curves { get; }

        public CreasePath(XYZ corner, XYZ drain, List<Curve> curves)
        {
            Corner = corner;
            Drain = drain;
            Curves = curves;
        }
    }
}
