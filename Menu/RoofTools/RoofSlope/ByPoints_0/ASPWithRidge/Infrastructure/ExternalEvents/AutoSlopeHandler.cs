// =======================================================
// File: AutoSlopeHandler.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.WithRidge
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Engine;
using Revit26_Plugin.AutoSlopeByPoint.WithRidge.Core.Models;
using System;

namespace Revit26_Plugin.AutoSlopeByPoint.WithRidge.Infrastructure.ExternalEvents
{
    public class AutoSlopeHandler : IExternalEventHandler
    {
        public static AutoSlopePayload Payload;

        public void Execute(UIApplication app)
        {
            if (Payload == null) return;

            AutoSlopePayload current = Payload;

            using (TransactionGroup tg = new TransactionGroup(
                app.ActiveUIDocument.Document, "AutoSlope With Ridge"))
            {
                tg.Start();

                try
                {
                    AutoSlopeEngine.Execute(app, current);
                    tg.Assimilate();
                }
                catch (Exception ex)
                {
                    tg.RollBack();
                    current.Log?.Invoke($"[AutoSlopeHandler] Unhandled exception: {ex.Message}");
                    current.OnCompleted?.Invoke(new AutoSlopeResult
                    {
                        Success      = false,
                        ErrorMessage = $"Unhandled exception: {ex.Message}"
                    });
                }
            }
        }

        public string GetName() => "AutoSlope With Ridge Handler";
    }
}
