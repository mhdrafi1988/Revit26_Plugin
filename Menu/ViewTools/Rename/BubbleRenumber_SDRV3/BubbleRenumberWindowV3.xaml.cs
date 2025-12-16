using System;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;

namespace Revit22_Plugin.SDRV3
{
    public partial class BubbleRenumberWindowV3 : MahApps.Metro.Controls.MetroWindow
    {
        public BubbleRenumberWindowV3(UIDocument uidoc, UIApplication uiapp)
        {
            InitializeComponent();
            DataContext = new BubbleRenumberViewModelV3(uidoc);

            IntPtr hwnd = uiapp.MainWindowHandle;
            new WindowInteropHelper(this) { Owner = hwnd };
        }
    }
}
