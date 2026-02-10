// File: AutoPlaceSectionsEventManager.cs
using Autodesk.Revit.UI;

namespace Revit26_Plugin.APUS_V314.ExternalEvents
{
    public static class AutoPlaceSectionsEventManager
    {
        private static AutoPlaceSectionsHandler _handler;
        private static ExternalEvent _externalEvent;

        public static AutoPlaceSectionsHandler Handler
        {
            get
            {
                if (_handler == null)
                    Initialize();
                return _handler;
            }
            set => _handler = value;
        }

        public static ExternalEvent ExternalEvent
        {
            get
            {
                if (_externalEvent == null)
                    Initialize();
                return _externalEvent;
            }
        }

        public static void Initialize()
        {
            if (_handler != null && _externalEvent != null)
                return;

            _handler = new AutoPlaceSectionsHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public static void Cleanup()
        {
            _handler = null;
            _externalEvent = null;
        }
    }
}