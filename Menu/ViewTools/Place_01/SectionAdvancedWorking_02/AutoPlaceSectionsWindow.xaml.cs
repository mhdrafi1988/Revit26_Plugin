using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;

namespace Revit22_Plugin.SectionPlacer.MVVM
{
    public partial class AutoPlaceSectionsWindow : Window
    {
        public AutoPlaceSectionsWindow(UIDocument uidoc)
        {
            InitializeComponent();
            DataContext = new Revit22_Plugin.SectionPlacer.ViewModels.AutoPlaceSectionsViewModel(uidoc);

            // Get Revit main window handle using Process (fallback approach)
            IntPtr revitHwnd = Process.GetCurrentProcess().MainWindowHandle;

            // Set this WPF window to be OWNED by Revit (keeps it on top of Revit)
            var interop = new WindowInteropHelper(this) { Owner = revitHwnd };

            // Center relative to owner and bring to front when shown
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.ShowInTaskbar = false;
            this.Loaded += (s, e) => { this.Activate(); };

            // Optional: if you want to “nudge” it to front anytime it deactivates:
            // this.Deactivated += (s, e) => { this.Topmost = true; this.Topmost = false; };
        }
    }
}
