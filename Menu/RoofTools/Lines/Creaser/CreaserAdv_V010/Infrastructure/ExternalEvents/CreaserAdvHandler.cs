// =======================================================
// File: CreaserAdvHandler.cs
// Location: Infrastructure/ExternalEvents/
// Mirrors InnerLoopDividerHandler's pattern — Run() mutates the document,
// so it's wrapped in a TransactionGroup so a failure rolls back cleanly.
// CreaserAdvEngine opens its own inner Transaction only for the placement
// step (standard nested Transaction/TransactionGroup pattern).
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv.V010.Core.Models;
using System;

namespace Revit26_Plugin.CreaserAdv.V010.Infrastructure.ExternalEvents
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
            // Take the payload and clear the slot so a stray second Raise() can
            // never re-run a request that has already been handled.
            CreaserAdvPayload current = Payload;
            Payload = null;

            if (current == null) return;

            using (TransactionGroup tg = new TransactionGroup(
                app.ActiveUIDocument.Document, "Creaser Advanced"))
            {
                tg.Start();

                try
                {
                    var result = Core.Engine.CreaserAdvEngine.Execute(app, current);

                    // Nothing placed => nothing to merge; an empty group is rolled back
                    // rather than assimilated.
                    if (result.Success && result.Created > 0)
                        tg.Assimilate();
                    else
                        tg.RollBack();

                    current.OnCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    if (tg.GetStatus() == TransactionStatus.Started)
                        tg.RollBack();

                    current.Log?.Error($"[CreaserAdvHandler] Unhandled {ex.GetType().Name}: {ex.Message}");
                    current.Log?.Debug(ex.StackTrace ?? "(no stack trace)");
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
