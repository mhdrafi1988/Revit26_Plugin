// File: AutoPlaceSectionsWindow.xaml.cs
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V318.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Revit26_Plugin.APUS_V318.Views
{
    public partial class AutoPlaceSectionsWindow : Window
    {
        private DispatcherTimer _logScrollTimer;

        public AutoPlaceSectionsWindow(AutoPlaceSectionsViewModel viewModel)
        {
            InitializeComponent();

            try
            {
                // Set DataContext (ViewModel has NO Revit API calls)
                DataContext = viewModel;

                // Set Revit as owner for proper modal behavior
                IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
                var helper = new WindowInteropHelper(this)
                {
                    Owner = revitHandle
                };

                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ShowInTaskbar = false;

                // Ensure window activates when loaded
                Loaded += (sender, e) =>
                {
                    Activate();
                    StartLogAutoScroll();
                };

                // Handle closing
                Closing += OnWindowClosing;

                // Listen for collection changes to auto-scroll
                if (viewModel.LogEntries is System.Collections.Specialized.INotifyCollectionChanged notifyCollection)
                {
                    notifyCollection.CollectionChanged += (s, e) =>
                    {
                        ScrollLogToBottom();
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize window: {ex.Message}",
                    "APUS V314 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void StartLogAutoScroll()
        {
            _logScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _logScrollTimer.Tick += (s, e) => ScrollLogToBottom();
            _logScrollTimer.Start();
        }

        private void ScrollLogToBottom()
        {
            if (LogListBox != null && LogListBox.Items.Count > 0)
            {
                LogListBox.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    try
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                    }
                    catch { }
                }));
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _logScrollTimer?.Stop();
            _logScrollTimer = null;

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