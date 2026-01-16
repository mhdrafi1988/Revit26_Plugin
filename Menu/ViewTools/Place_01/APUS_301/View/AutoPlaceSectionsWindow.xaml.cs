using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.APUS_301.MVVM
{
    /// <summary>
    /// Interaction logic for AutoPlaceSectionsWindow.xaml
    /// Pure view wiring only. No business logic here.
    /// </summary>
    public partial class AutoPlaceSectionsWindow : Window
    {
        public AutoPlaceSectionsWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // -----------------------------
            // DataContext (MVVM)
            // -----------------------------
            DataContext = new Revit26_Plugin.APUS_301.ViewModels.AutoPlaceSectionsViewModel(uidoc);

            // -----------------------------
            // Set Revit as window owner
            // Keeps window on top of Revit
            // -----------------------------
            IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
            new WindowInteropHelper(this)
            {
                Owner = revitHandle
            };

            // -----------------------------
            // Window behavior
            // -----------------------------
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            // Bring to front when shown
            Loaded += (_, __) => Activate();
        }
    }
}
