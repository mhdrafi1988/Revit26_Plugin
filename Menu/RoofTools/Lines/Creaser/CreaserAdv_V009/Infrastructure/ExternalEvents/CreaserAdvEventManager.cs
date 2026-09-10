using Autodesk.Revit.UI;

namespace Revit26_Plugin.CreaserAdv.V009.Infrastructure.ExternalEvents
{
    public static class CreaserAdvEventManager
    {
        public static CreaserAdvHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Init()
        {
            if (Handler != null) return;
            Handler = new CreaserAdvHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
