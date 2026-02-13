using Autodesk.Revit.UI;

namespace Revit26_Plugin.APUS_V313.ExternalEvents
{
    public static class AutoPlaceSectionsEventManager
    {
        private static readonly AutoPlaceSectionsExternalEventHandler _handler;
        private static ExternalEvent _externalEvent;

        static AutoPlaceSectionsEventManager()
        {
            _handler = new AutoPlaceSectionsExternalEventHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public static ExternalEvent ExternalEvent => _externalEvent;
        public static AutoPlaceSectionsExternalEventHandler Handler => _handler;
    }
}