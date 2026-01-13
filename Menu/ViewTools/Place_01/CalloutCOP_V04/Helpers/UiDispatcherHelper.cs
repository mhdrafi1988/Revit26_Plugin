using System;
using System.Windows;

namespace Revit26_Plugin.CalloutCOP_V04.Helpers
{
    public static class UiDispatcherHelper
    {
        public static void Invoke(Action action)
        {
            if (Application.Current.Dispatcher.CheckAccess())
                action();
            else
                Application.Current.Dispatcher.Invoke(action);
        }
    }
}
