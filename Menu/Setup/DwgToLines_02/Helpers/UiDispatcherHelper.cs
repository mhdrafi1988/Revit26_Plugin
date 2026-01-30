using System;
using System.Windows.Threading;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Helpers
{
    public static class UiDispatcherHelper
    {
        private static Dispatcher _dispatcher;

        public static void Initialize(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public static void Run(Action action)
        {
            if (_dispatcher == null)
            {
                action();
                return;
            }

            if (_dispatcher.CheckAccess())
                action();
            else
                _dispatcher.BeginInvoke(action);
        }
    }
}
