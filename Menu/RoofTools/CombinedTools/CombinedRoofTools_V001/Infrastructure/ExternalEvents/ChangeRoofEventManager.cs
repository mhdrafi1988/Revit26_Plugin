using Autodesk.Revit.UI;

namespace Revit26_Plugin.CombinedRoofTools.V001.Infrastructure.ExternalEvents
{
    public static class ChangeRoofEventManager
    {
        public static ChangeRoofHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Init()
        {
            if (Handler != null) return;
            Handler = new ChangeRoofHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
