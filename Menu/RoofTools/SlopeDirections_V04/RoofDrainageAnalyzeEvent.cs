using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace Revit_26.CornertoDrainArrow_V05
{
    public sealed class RoofDrainageAnalyzeEvent : IExternalEventHandler
    {
        private readonly RoofDrainageCoordinator _coordinator;
        private readonly ILogService _logService;

        public ElementId RoofId { get; set; }

        // UI callback
        public Action<RoofDrainageAnalysisDto> OnCompleted { get; set; }

        public RoofDrainageAnalyzeEvent(ILogService logService)
        {
            _logService = logService;
            _coordinator = new RoofDrainageCoordinator(logService);
        }

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;

            using var tx = new Transaction(doc, "Analyze Roof Drainage");
            tx.Start();

            var analysis = _coordinator.AnalyzeRoofDetailed(doc, RoofId);

            tx.Commit();

            OnCompleted?.Invoke(analysis);
        }

        public string GetName() => "Roof Drainage Analyze Event";
    }
}
