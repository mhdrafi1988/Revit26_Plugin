using System.Windows.Threading;

namespace Revit26_Plugin.CalloutCOP.V019.Helpers
{
    /// <summary>
    /// The window is modeless and shares Revit's UI thread with the
    /// ExternalEvent handler, so while the handler runs WPF cannot repaint.
    /// Calling DoEvents() right after updating progress properties drains
    /// pending render/input work so the bar and the disabled Run button
    /// actually appear mid-run. Call only from the UI thread.
    /// </summary>
    public static class UiPumpHelper
    {
        public static void DoEvents()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame),
                frame);
            Dispatcher.PushFrame(frame);
        }

        private static object ExitFrame(object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }
    }
}
