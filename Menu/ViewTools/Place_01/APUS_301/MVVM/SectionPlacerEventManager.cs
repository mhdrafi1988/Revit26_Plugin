using Autodesk.Revit.UI;

namespace Revit26_Plugin.APUS_301.MVVM
{
    /// <summary>
    /// Manages the ExternalEvent bridge between WPF and Revit API.
    /// </summary>
    public static class SectionPlacerEventManager
    {
        public static AutoPlaceSectionsHandler PlaceHandler { get; private set; }
        public static ExternalEvent PlaceEvent { get; private set; }

        public static void Initialize()
        {
            if (PlaceHandler != null && PlaceEvent != null)
                return;

            PlaceHandler = new AutoPlaceSectionsHandler();
            PlaceEvent = ExternalEvent.Create(PlaceHandler);
        }
    }
}
