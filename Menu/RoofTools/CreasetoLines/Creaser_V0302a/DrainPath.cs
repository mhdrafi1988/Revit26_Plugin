using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V03_03.Models
{
    public class DrainPath
    {
        public XYZ Corner { get; }
        public XYZ Drain { get; }
        public List<Line> Lines { get; }

        public DrainPath(XYZ corner, XYZ drain, List<Line> lines)
        {
            Corner = corner;
            Drain = drain;
            Lines = lines;
        }
    }
}
