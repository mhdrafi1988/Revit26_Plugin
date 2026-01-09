using Autodesk.Revit.UI;

namespace Revit22_Plugin.AutoRoofSections.MVVM
{
    public static class RoofSectionsEventManager
    {
        public static RoofSectionsHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Initialize()
        {
            if (Handler != null && Event != null)
                return;

            Handler = new RoofSectionsHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
