using System;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofViewFocus.V001.Core.Models;

namespace Revit26_Plugin.RoofViewFocus.V001.Infrastructure.ExternalEvents
{
    /// <summary>
    /// Static holder for the tool's single Handler + ExternalEvent pair.
    /// Init() is idempotent (repo-wide pattern: called from the ViewModel constructor,
    /// which runs inside Command.Execute — a valid Revit API context).
    /// </summary>
    public static class RoofViewFocusEventManager
    {
        public static RoofViewFocusHandler? Handler { get; private set; }
        public static ExternalEvent? Event { get; private set; }

        public static void Init()
        {
            if (Handler != null) return;
            Handler = new RoofViewFocusHandler();
            Event = ExternalEvent.Create(Handler);
        }

        /// <summary>
        /// Stores the payload on the handler and raises the event.
        /// Returns false (with the Revit status text) when Revit refuses the request.
        /// </summary>
        public static bool TryRaise(FocusRequest request, out string status)
        {
            if (Handler == null || Event == null)
                throw new InvalidOperationException("RoofViewFocusEventManager.Init() has not been called.");

            RoofViewFocusHandler.PendingRequest = request;
            ExternalEventRequest raised = Event.Raise();
            status = raised.ToString();

            bool ok = raised == ExternalEventRequest.Accepted || raised == ExternalEventRequest.Pending;
            if (!ok) RoofViewFocusHandler.PendingRequest = null;
            return ok;
        }
    }
}
