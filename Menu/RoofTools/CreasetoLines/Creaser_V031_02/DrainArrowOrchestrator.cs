using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Creaser_V31.Models;

namespace Revit26_Plugin.Creaser_V31.Services
{
    /// <summary>
    /// Coordinates geometry extraction, path solving,
    /// and arrow placement inside a single transaction.
    /// </summary>
    public class DrainArrowOrchestrator
    {
        public void Execute(UIDocument uiDoc)
        {
            Document doc = uiDoc.Document;

            using Transaction tx =
                new Transaction(doc, "Place Roof Drain Slope Arrows");

            tx.Start();

            RoofGeometryService geometryService = new();
            DrainGraphService graphService = new();
            ArrowPlacementService placementService = new();

            var roofData = geometryService.Extract(doc);
            var graphData = graphService.Build(roofData);

            placementService.Place(doc, graphData);  // Now this works - both in same assembly

            tx.Commit();
        }
    }
}