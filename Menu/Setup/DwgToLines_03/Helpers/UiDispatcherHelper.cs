// ==============================================
// File: UiDispatcherHelper.cs
// Layer: Helpers
// ==============================================

using System;
using System.Windows.Threading;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Helpers
{
    /// <summary>
    /// Ensures UI updates occur on the correct dispatcher thread.
    /// </summary>
    public static class UiDispatcherHelper
    {
        private static Dispatcher _dispatcher;

        /// <summary>
        /// Initializes dispatcher from the UI thread.
        /// Call once from View code-behind.
        /// </summary>
        public static void Initialize(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// Executes an action on the UI thread safely.
        /// </summary>
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
