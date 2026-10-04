using Autodesk.Revit.UI;

namespace Revit26_Plugin.InnerLoopDivider.V010.Infrastructure.ExternalEvents
{
    public static class InnerLoopDividerEventManager
    {
        public static InnerLoopDividerHandler Handler { get; private set; }
        public static ExternalEvent Event { get; private set; }

        public static void Init()
        {
            if (Handler != null) return;
            Handler = new InnerLoopDividerHandler();
            Event = ExternalEvent.Create(Handler);
        }
    }
}
