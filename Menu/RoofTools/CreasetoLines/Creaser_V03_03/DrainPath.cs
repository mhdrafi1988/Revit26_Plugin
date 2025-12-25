using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V03_03.Models
{
    public class DrainPath
    {
        public XYZ Start { get; }
        public XYZ End { get; }
        public List<Line> Lines { get; }

        public DrainPath(XYZ start, XYZ end, List<Line> lines)
        {
            Start = start;
            End = end;
            Lines = lines;
        }
    }
}
