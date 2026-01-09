using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using Revit22_Plugin.AutoRoofSections.ViewModels;

namespace Revit22_Plugin.AutoRoofSections.MVVM
{
    public partial class RoofSectionWindow : Window
    {
        public RoofSectionWindow(UIDocument uidoc, UIApplication uiapp)
        {
            InitializeComponent();

            // MVVM
            DataContext = new RoofSectionViewModel(uidoc, uiapp);

            // Attach Revit window as owner
            IntPtr hwnd = uiapp?.MainWindowHandle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero)
                hwnd = Process.GetCurrentProcess().MainWindowHandle;

            new WindowInteropHelper(this) { Owner = hwnd };

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            Loaded += (_, __) => Activate();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
