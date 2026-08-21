// =======================================================
// File: InnerLoopDividerEngine.cs
// Location: Core/Engine/
// New in V009. Orchestration only — dispatches to RoofGeometryService
// (Analyze) or LoopDivisionService (Apply) based on payload.Operation.
// Called by InnerLoopDividerHandler from inside a valid Revit API
// context (Analyze is read-only; Apply runs inside the Handler's
// TransactionGroup).
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.InnerLoopDivider.V009.Core.Models;
using Revit26_Plugin.InnerLoopDivider.V009.Core.Services;
using Revit26_Plugin.Shared.Models;
using System.Linq;

namespace Revit26_Plugin.InnerLoopDivider.V009.Core.Engine
{
    public static class InnerLoopDividerEngine
    {
        public static InnerLoopDividerResult Execute(UIApplication app, InnerLoopDividerPayload payload)
        {
            var log = payload.Log ?? (_ => { });
            Document doc = app.ActiveUIDocument.Document;

            RoofBase roof = doc.GetElement(payload.RoofId) as RoofBase;
            if (roof == null)
            {
                const string msg = "Roof element not found (it may have been deleted).";
                log(new LogEntry(LogLevel.Error, msg));
                return new InnerLoopDividerResult
                {
                    Success = false,
                    ErrorMessage = msg,
                    Operation = payload.Operation
                };
            }

            if (payload.Operation == InnerLoopDividerOperation.Analyze)
                return ExecuteAnalyze(roof, log);

            return ExecuteApply(doc, roof, payload, log);
        }

        private static InnerLoopDividerResult ExecuteAnalyze(RoofBase roof, System.Action<LogEntry> log)
        {
            var geometryService = new RoofGeometryService();
            var innerLoops = geometryService
                .ExtractCircularLoops(roof)
                .Where(l => l.LoopType == "Inner")
                .ToList();

            return new InnerLoopDividerResult
            {
                Success = true,
                Operation = InnerLoopDividerOperation.Analyze,
                Loops = innerLoops
            };
        }

        private static InnerLoopDividerResult ExecuteApply(
            Document doc, RoofBase roof, InnerLoopDividerPayload payload, System.Action<LogEntry> log)
        {
            var divisionService = new LoopDivisionService();
            int placed = divisionService.AddDivisionPoints(doc, roof, payload.SelectedLoops);

            if (placed >= 0)
            {
                log(new LogEntry(LogLevel.Success,
                    $"Done — {placed} division point(s) added to {payload.SelectedLoops.Count} loop(s)."));

                return new InnerLoopDividerResult
                {
                    Success = true,
                    Operation = InnerLoopDividerOperation.Apply,
                    PointsAdded = placed
                };
            }

            const string failMsg = "Apply failed — transaction error.";
            log(new LogEntry(LogLevel.Error, failMsg));
            return new InnerLoopDividerResult
            {
                Success = false,
                ErrorMessage = failMsg,
                Operation = InnerLoopDividerOperation.Apply,
                PointsAdded = -1
            };
        }
    }
}
