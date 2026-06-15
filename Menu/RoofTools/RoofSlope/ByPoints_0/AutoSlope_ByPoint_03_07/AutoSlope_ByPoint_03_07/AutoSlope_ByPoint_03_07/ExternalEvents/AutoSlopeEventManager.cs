using Autodesk.Revit.UI;
namespace Revit26_Plugin.AutoSlopeByPoint_30_07.ExternalEvents
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
