using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofPointComparison.V001.Infrastructure.ExternalEvents
{
    public static class RoofComparisonEventManager
    {
        public static RoofComparisonHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Init()
        {
            if (Handler != null) return;
            Handler = new RoofComparisonHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
