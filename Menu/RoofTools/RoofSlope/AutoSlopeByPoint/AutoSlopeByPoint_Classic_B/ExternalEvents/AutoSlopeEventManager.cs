using Autodesk.Revit.UI;
//using Revit22_Plugin.AutoSlopeByPoint.Handlers;

namespace Revit26_Plugin.AutoSlopeByPoint.ExternalEvents
{
    public static class AutoSlopeEventManager
    {
        public static AutoSlopeHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Init()
        {
            if (Handler != null) return;
            Handler = new AutoSlopeHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
