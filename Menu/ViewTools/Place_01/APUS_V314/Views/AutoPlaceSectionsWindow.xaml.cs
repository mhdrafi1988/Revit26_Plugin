// File: AutoPlaceSectionsWindow.xaml.cs
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace Revit26_Plugin.APUS_V314.Views
{
    public partial class AutoPlaceSectionsWindow : Window
    {
        public AutoPlaceSectionsWindow(UIDocument uidoc)
        {
            InitializeComponent();

            try
            {
                // Initialize ViewModel
                var viewModel = new AutoPlaceSectionsViewModel(uidoc);
                DataContext = viewModel;

                // Set Revit as owner for proper modal behavior
                IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
                var helper = new WindowInteropHelper(this)
                {
                    Owner = revitHandle
                };

                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ShowInTaskbar = false;

                // CHANGED: Initialize data AFTER window is fully loaded
                Loaded += (sender, e) =>
                {
                    viewModel.InitializeData();
                    Activate();
                };

                // Handle closing
                Closing += OnWindowClosing;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize window: {ex.Message}",
                    "APUS V314 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is AutoPlaceSectionsViewModel vm)
            {
                // Cancel any ongoing operation
                if (vm.IsProcessing)
                {
                    var result = MessageBox.Show(
                        "Placement is in progress. Are you sure you want to cancel and close?",
                        "Confirm Close",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        vm.Progress.Cancel();
                        vm.LogWarning("Window closed by user during placement.");
                    }
                    else
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                // Clean up resources
                vm.OnWindowClosing();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}