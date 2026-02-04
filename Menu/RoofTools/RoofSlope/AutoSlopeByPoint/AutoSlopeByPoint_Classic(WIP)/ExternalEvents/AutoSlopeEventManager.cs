using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Models;
using System;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.ExternalEvents
{
    public static class AutoSlopeEventManager
    {
        public static ExternalEvent Event { get; private set; }
        public static IExternalEventHandler Handler { get; private set; }
        public static AutoSlopePayload Payload { get; set; }

        public static void Init(Document doc)
        {
            if (Event != null && Handler != null)
                return;

            if (doc == null)
                throw new ArgumentNullException(
                    nameof(doc),
                    "AutoSlopeEventManager.Init failed: Document is NULL");

            AutoSlopeExternalEventHandler handler;
            try
            {
                handler = new AutoSlopeExternalEventHandler(doc);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to create AutoSlopeExternalEventHandler",
                    ex);
            }

            if (handler == null)
                throw new InvalidOperationException(
                    "AutoSlopeExternalEventHandler constructor returned NULL");

            Handler = handler;

            try
            {
                Event = ExternalEvent.Create(Handler);
            }
            catch (Exception ex)
            {
                Handler = null;
                Event = null;

                throw new InvalidOperationException(
                    "ExternalEvent.Create failed. Handler was not accepted by Revit.",
                    ex);
            }
        }
    }
}