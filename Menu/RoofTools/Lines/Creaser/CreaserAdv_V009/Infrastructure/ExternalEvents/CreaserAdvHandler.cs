// =======================================================
// File: CreaserAdvHandler.cs
// Location: Infrastructure/ExternalEvents/
// Mirrors InnerLoopDividerHandler's pattern — Run() mutates the document,
// so it's wrapped in a TransactionGroup so a failure rolls back cleanly.
// CreaserAdvEngine still opens its own inner Transaction (standard nested
// Transaction/TransactionGroup pattern).
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv.V009.Core.Models;
using System;

namespace Revit26_Plugin.CreaserAdv.V009.Infrastructure.ExternalEvents
{
    public class CreaserAdvHandler : IExternalEventHandler
    {
        /// <summary>
        /// Set by the ViewModel immediately before raising the ExternalEvent.
        /// Static because IExternalEventHandler instances are created once by
        /// Revit and cannot receive constructor arguments per-invocation.
        /// </summary>
        public static CreaserAdvPayload Payload;

        public void Execute(UIApplication app)
        {
            if (Payload == null) return;

            CreaserAdvPayload current = Payload;

            using (TransactionGroup tg = new TransactionGroup(
                app.ActiveUIDocument.Document, "Creaser Advanced"))
            {
                tg.Start();

                try
                {
                    var result = Core.Engine.CreaserAdvEngine.Execute(app, current);

                    if (result.Success)
                        tg.Assimilate();
                    else
                        tg.RollBack();

                    current.OnCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    tg.RollBack();
                    current.Log?.Error($"[CreaserAdvHandler] Unhandled exception: {ex.Message}");
                    current.OnCompleted?.Invoke(new CreaserAdvResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }
        }

        public string GetName() => "Creaser Adv Handler";
    }
}
