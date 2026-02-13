using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V313.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.APUS_V313.Views
{
    public partial class AutoPlaceSectionsWindow : Window
    {
        public AutoPlaceSectionsWindow(UIDocument uidoc)
        {
            InitializeComponent();

            // Attach ViewModel
            var viewModel = new AutoPlaceSectionsViewModel(uidoc);
            DataContext = viewModel;

            // Ensure Revit owns this window
            IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;

            new WindowInteropHelper(this)
            {
                Owner = revitHandle
            };

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            // Load data asynchronously when window is loaded
            Loaded += async (_, __) =>
            {
                try
                {
                    Activate();
                    await viewModel.LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load data: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

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