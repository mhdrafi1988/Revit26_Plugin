using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit_26.CornertoDrainArrow_V05
{
    public sealed class RoofDrainageExternalEventHandler : IExternalEventHandler
    {
        private readonly RoofDrainageCoordinator _coordinator;
        private readonly ElementId _roofId;
        private RoofDrainageRunResult _result;

        public RoofDrainageExternalEventHandler(RoofDrainageCoordinator coordinator, ElementId roofId)
        {
            _coordinator = coordinator;
            _roofId = roofId;
        }

        public RoofDrainageRunResult Result => _result;

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;

            var doc = uidoc.Document;

            using var tx = new Transaction(doc, "Analyze Roof Drainage");
            tx.Start();

            var analysisDto = _coordinator.AnalyzeRoofDetailed(doc, _roofId);
            _result = new RoofDrainageRunResult
            {
                CornerCount = analysisDto.Summary.CornerCount,
                DrainCount = analysisDto.Summary.DrainCount,
                TotalPaths = analysisDto.Summary.TotalPaths,
                ValidPaths = analysisDto.Summary.ValidPaths,
                PlacedDetails = analysisDto.Summary.PlacedDetails
            };

            tx.Commit();
        }

        public string GetName() => "Corner To Drain Roof Drainage Handler";
    }
}