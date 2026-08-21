using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofDetailLineIntersect.V011.Infrastructure.ExternalEvents
{
    public static class RoofDetailLineIntersectEventManager
    {
        public static RoofDetailLineIntersectHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Init()
        {
            if (Handler != null) return;
            Handler = new RoofDetailLineIntersectHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
