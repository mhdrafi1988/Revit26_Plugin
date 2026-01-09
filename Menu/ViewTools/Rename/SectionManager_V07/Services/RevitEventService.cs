using Autodesk.Revit.UI;

namespace Revit26_Plugin.SectionManager_V07.Services
{
    public class RevitEventService
    {
        public ExternalEvent ExternalEvent { get; }

        public RevitEventService(IExternalEventHandler handler)
        {
            ExternalEvent = ExternalEvent.Create(handler);
        }
    }
}
