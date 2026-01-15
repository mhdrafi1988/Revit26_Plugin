using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Models
{
    public class SimplePipelineResult
    {
        public IList<XYZ> Corners { get; set; }
        public IList<XYZ> Drains { get; set; }
        public IList<Line> CreaseLines { get; set; }
    }
}
