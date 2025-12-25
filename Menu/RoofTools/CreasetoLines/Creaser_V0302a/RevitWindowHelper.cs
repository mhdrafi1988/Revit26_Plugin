using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.Creaser_V03_03.Helpers
{
    public static class RevitWindowHelper
    {
        public static void SetOwner(Window window, UIApplication uiApp)
        {
            IntPtr revitHandle = uiApp.MainWindowHandle;
            WindowInteropHelper helper = new WindowInteropHelper(window);
            helper.Owner = revitHandle;
        }
    }
}
