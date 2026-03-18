using System;
using System.Windows;

namespace Revit26_Plugin.AddPointOnintersections.Helpers
{
    public static class WpfDispatcherHelper
    {
        public static void SafeInvoke(Action action)
        {
            if (Application.Current == null)
            {
                action?.Invoke();
                return;
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                action?.Invoke();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(action);
            }
        }
    }
}