using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.V006.Models
{
    public class ProfileLoop
    {
        public ProfileSourceType Source { get; set; }
        public List<Curve> Curves { get; set; } = new();
    }
}
