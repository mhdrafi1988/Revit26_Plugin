using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V31.Models;

namespace Revit26_Plugin.Creaser_V31.Services
{
    /// <summary>
    /// Responsible ONLY for extracting usable roof geometry.
    /// No path logic. No placement logic.
    /// </summary>
    public class RoofGeometryService
    {
        internal RoofGeometryData Extract(Document doc)  // Changed from public to internal
        {
            // TODO:
            // - Collect roof elements
            // - Extract boundary + slope directions
            // - Populate XYZKey nodes

            return new RoofGeometryData();
        }
    }
}