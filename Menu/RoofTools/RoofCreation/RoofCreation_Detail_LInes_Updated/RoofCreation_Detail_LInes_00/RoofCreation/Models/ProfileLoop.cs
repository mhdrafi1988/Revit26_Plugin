using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.V008.V007.Models
{
    public class ProfileLoop
    {
        public ProfileSourceType Source { get; set; }
        public List<Curve> Curves { get; set; } = new();
    }
}
