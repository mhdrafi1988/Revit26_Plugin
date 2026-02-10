using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V311.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.APUS_V311.Views
{
    /// <summary>
    /// Interaction logic for AutoPlaceSectionsWindow.xaml
    /// Code-behind responsibilities:
    /// - ViewModel wiring
    /// - Revit window ownership
    /// - Safe shutdown handling
    /// NO business logic here.
    /// </summary>
    public partial class AutoPlaceSectionsWindow : Window
    {
        /// <summary>
        /// REQUIRED constructor.
        /// Called from IExternalCommand.
        /// </summary>
        public AutoPlaceSectionsWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // -------------------------------
            // Attach ViewModel
            // -------------------------------
            DataContext = new AutoPlaceSectionsViewModel(uidoc);

            // -------------------------------
            // Ensure Revit owns this window
            // Prevents focus / z-order bugs
            // -------------------------------
            IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;

            new WindowInteropHelper(this)
            {
                Owner = revitHandle
            };

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            Loaded += (_, __) => Activate();
        }

        /// <summary>
        /// Ensure placement cancels safely if user closes window.
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
