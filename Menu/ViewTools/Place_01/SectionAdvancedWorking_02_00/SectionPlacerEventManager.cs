using Autodesk.Revit.UI;

namespace Revit22_Plugin.SectionPlacer.MVVM
{
    /// <summary>
    /// Manages creation and lifetime of the ExternalEvent
    /// that connects the WPF UI with Revit's API thread.
    /// </summary>
    public static class SectionPlacerEventManager
    {
        public static AutoPlaceSectionsHandler PlaceHandler { get; private set; }
        public static ExternalEvent PlaceEvent { get; private set; }

        /// <summary>
        /// Initializes the handler and external event only once.
        /// Safe to call multiple times.
        /// </summary>
        public static void Initialize()
        {
            // Thread-safe double-check lock
            if (PlaceHandler != null && PlaceEvent != null)
                return;

            PlaceHandler = new AutoPlaceSectionsHandler();
            PlaceEvent = ExternalEvent.Create(PlaceHandler);
        }

        /// <summary>
        /// Optional method to reset the handler if needed (rare).
        /// </summary>
        public static void Reset()
        {
            PlaceHandler = new AutoPlaceSectionsHandler();
            PlaceEvent = ExternalEvent.Create(PlaceHandler);
        }
    }
}
