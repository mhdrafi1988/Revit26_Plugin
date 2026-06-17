using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Models
{
    /// <summary>
    /// Internal working model for a single boundary loop (outer or one interior loop)
    /// used by BoundaryProjectionService. Not bound directly to the UI.
    /// </summary>
    public class LoopBoundaryModel
    {
        public string      Label  { get; set; }   // "Outer", "Inner #1", "Inner #2", ...
        public List<Curve> Curves { get; set; }
    }
}
