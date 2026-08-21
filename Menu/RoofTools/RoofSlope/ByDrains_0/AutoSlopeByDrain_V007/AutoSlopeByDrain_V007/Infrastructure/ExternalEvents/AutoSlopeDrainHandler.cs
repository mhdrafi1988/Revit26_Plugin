// File: AutoSlopeDrainHandler.cs
// Location: Infrastructure/ExternalEvents/
// Ported unchanged from AutoSlopeByDrain V004.

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByDrain.V007.Core.Engine;
using Revit26_Plugin.AutoSlopeByDrain.V007.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;

namespace Revit26_Plugin.AutoSlopeByDrain.V007.Infrastructure.ExternalEvents
{
    public class AutoSlopeDrainHandler : IExternalEventHandler
    {
        /// <summary>
        /// Set by the ViewModel immediately before raising the ExternalEvent.
        /// Static because IExternalEventHandler instances are created once by
        /// Revit and cannot receive constructor arguments per-invocation.
        /// </summary>
        public static AutoSlopeDrainPayload Payload;

        public void Execute(UIApplication app)
        {
            if (Payload == null) return;

            AutoSlopeDrainPayload current = Payload;

            using (TransactionGroup tg = new TransactionGroup(
                app.ActiveUIDocument.Document, "AutoSlope By Drain"))
            {
                tg.Start();

                try
                {
                    var result = AutoSlopeDrainEngine.Execute(app, current);

                    if (result.Success)
                        tg.Assimilate();
                    else
                        tg.RollBack();

                    current.OnCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    tg.RollBack();
                    current.Log?.Invoke(new LogEntry(LogLevel.Error,
                        $"[AutoSlopeDrainHandler] Unhandled exception: {ex.Message}"));
                    current.OnCompleted?.Invoke(new AutoSlopeDrainResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }
        }

        public string GetName() => "AutoSlope By Drain Handler";
    }
}
