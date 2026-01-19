using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V306.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.APUS_V306.Views
{
    /// <summary>
    /// Interaction logic for AutoPlaceSectionsWindow.xaml
    /// Code-behind is LIMITED to:
    /// - Window ownership
    /// - ViewModel wiring
    /// No business logic lives here.
    /// </summary>
    public partial class AutoPlaceSectionsWindow : Window
    {
        public AutoPlaceSectionsWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // ? Attach ViewModel
            DataContext = new AutoPlaceSectionsViewModel(uidoc);

            // ? Ensure Revit owns the window (prevents focus & z-order issues)
            IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
            new WindowInteropHelper(this)
            {
                Owner = revitHandle
            };

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            // Optional: bring to front on load
            Loaded += (_, __) => Activate();
        }

        /// <summary>
        /// Ensures cancel is requested if user closes the window mid-run.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is AutoPlaceSectionsViewModel vm)
            {
                vm.Progress.Cancel();
                vm.LogWarning("Window closed by user.");
            }

            base.OnClosed(e);
        }
    }
}
