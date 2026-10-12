// =======================================================
// File: CurveDividerEngine.cs
// Location: Core/Engine/
// New in V004. Orchestration only — resolves the roof from RoofId and
// delegates to CurveDivisionService.ApplyDivisions. Called by
// CurveDividerHandler from inside its TransactionGroup.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.OuterCurveDivider.V004.Core.Services;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.OuterCurveDivider.V004.Core.Models;

namespace Revit26_Plugin.OuterCurveDivider.V004.Core.Engine
{
    public static class CurveDividerEngine
    {
        public static CurveDividerResult Execute(UIApplication app, CurveDividerPayload payload)
        {
            Document doc = app.ActiveUIDocument.Document;

            RoofBase roof = doc.GetElement(payload.RoofId) as RoofBase;
            if (roof == null)
            {
                const string msg = "Roof element not found (it may have been deleted).";
                payload.Log?.Invoke(new LogEntry(LogLevel.Error, msg));
                return new CurveDividerResult { Success = false, ErrorMessage = msg };
            }

            var service = new CurveDivisionService();
            var log = service.ApplyDivisions(doc, roof, payload.SelectedEdges);

            return new CurveDividerResult { Success = true, LogEntries = log };
        }
    }
}
