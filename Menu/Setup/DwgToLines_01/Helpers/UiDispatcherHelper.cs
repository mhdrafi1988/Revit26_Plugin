using System;
using System.Windows;

namespace Revit26_Plugin.DwgSymbolicConverter_V01.Helpers
{
    public static class UiDispatcherHelper
    {
        public static void Run(Action action)
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }
}
