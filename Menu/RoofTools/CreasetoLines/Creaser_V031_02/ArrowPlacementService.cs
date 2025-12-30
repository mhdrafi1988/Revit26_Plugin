using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V31.Models;

namespace Revit26_Plugin.Creaser_V31.Services
{
    /// <summary>
    /// Places curve-based detail components in plan view.
    /// Pure placement – no geometry reasoning.
    /// </summary>
    public class ArrowPlacementService
    {
        // Change from public to internal to match DrainGraphData's accessibility
        internal void Place(Document doc, DrainGraphData data)
        {
            // Uses DrainPathSolver
            // Projects to sketch plane
            // Places family instances
        }
    }
}