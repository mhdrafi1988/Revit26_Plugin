using Autodesk.Revit.UI;

namespace Revit26_Plugin.APUS_V312.ExternalEvents
{
    /// <summary>
    /// Owns the ExternalEvent and handler instance for APUS.
    /// Initialize() must be called once (safe to call multiple times).
    /// </summary>
    public static class AutoPlaceSectionsEventManager
    {
        public static AutoPlaceSectionsHandler Handler { get; private set; }
        public static ExternalEvent ExternalEvent { get; private set; }

        public static void Initialize()
        {
            if (Handler != null && ExternalEvent != null)
                return;

            Handler = new AutoPlaceSectionsHandler();
            ExternalEvent = ExternalEvent.Create(Handler);
        }
    }
}
