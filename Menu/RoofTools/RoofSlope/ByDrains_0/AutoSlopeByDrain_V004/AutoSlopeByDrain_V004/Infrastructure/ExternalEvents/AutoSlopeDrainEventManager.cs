// File: AutoSlopeDrainEventManager.cs
// Location: Infrastructure/ExternalEvents/
// Mirrors AutoSlopeByPoint's AutoSlopeEventManager.
// Init() must be called once from IExternalApplication.OnStartup —
// NOT from the Command or the ViewModel constructor.

using Autodesk.Revit.UI;

namespace Revit26_Plugin.AutoSlopeByDrain.V004.Infrastructure.ExternalEvents
{
    public static class AutoSlopeDrainEventManager
    {
        public static AutoSlopeDrainHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Init()
        {
            if (Handler != null) return;
            Handler = new AutoSlopeDrainHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
