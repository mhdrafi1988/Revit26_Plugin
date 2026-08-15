using System.Windows;

namespace Revit26_Plugin.SectionManager.V008.Helpers
{
    public static class UiThreadDispatcher
    {
        public static void Invoke(System.Action action)
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }
}
