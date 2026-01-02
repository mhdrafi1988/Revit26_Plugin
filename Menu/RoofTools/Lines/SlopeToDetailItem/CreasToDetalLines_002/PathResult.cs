using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Models
{
    public class PathResult
    {
        public XYZ Start { get; set; }
        public XYZ End { get; set; }
        public IList<XYZ> OrderedNodes { get; set; }
        public double Length { get; set; }
    }
}
