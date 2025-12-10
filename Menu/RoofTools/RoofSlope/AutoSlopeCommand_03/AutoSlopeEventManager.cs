using Autodesk.Revit.UI;

namespace Revit22_Plugin.AutoSlopeV3.Handlers
{
    public static class AutoSlopeEventManager
    {
        public static AutoSlopeHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Initialize()
        {
            if (Handler != null) return;

            Handler = new AutoSlopeHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
