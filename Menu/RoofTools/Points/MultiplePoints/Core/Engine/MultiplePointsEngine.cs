// =======================================================
// File: MultiplePointsEngine.cs
// Location: Core/Engine/
// Orchestration only — resolves the roof from RoofId, rebuilds the
// settings snapshot carried on the payload, and delegates to
// EdgePointService.ApplyPoints. Called by MultiplePointsHandler from
// inside its TransactionGroup.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.MultiplePoints.V001.Core.Models;
using Revit26_Plugin.MultiplePoints.V001.Core.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.MultiplePoints.V001.Core.Engine
{
    public static class MultiplePointsEngine
    {
        public static MultiplePointsResult Execute(UIApplication app, MultiplePointsPayload payload)
        {
            Document doc = app.ActiveUIDocument.Document;

            RoofBase roof = doc.GetElement(payload.RoofId) as RoofBase;
            if (roof == null)
            {
                const string msg = "Roof element not found (it may have been deleted).";
                payload.Log?.Invoke(new LogEntry(LogLevel.Error, msg));
                return new MultiplePointsResult { Success = false, ErrorMessage = msg };
            }

            var settings = new MultiplePointsSettings
            {
                AddMidpoint         = payload.AddMidpoint,
                AddQuarterPoints    = payload.AddQuarterPoints,
                AddExtraOnLongEdges = payload.AddExtraOnLongEdges,
                ThresholdMeters     = payload.ThresholdMeters
            };

            var service = new EdgePointService();
            var log = service.ApplyPoints(doc, roof, payload.SelectedEdges, settings);

            return new MultiplePointsResult { Success = true, LogEntries = log };
        }
    }
}
