using System.Windows;

namespace Revit26_Plugin.SectionManager_V07.Helpers
{
    public static class UiThreadDispatcher
    {
        public static void Invoke(System.Action action)
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }
}
