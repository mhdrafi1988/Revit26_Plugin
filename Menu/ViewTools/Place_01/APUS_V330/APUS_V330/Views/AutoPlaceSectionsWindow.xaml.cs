// File: Views/AutoPlaceSectionsWindow.xaml.cs
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V330.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Revit26_Plugin.APUS_V330.Views
{
    public partial class AutoPlaceSectionsWindow : Window
    {
        private DispatcherTimer _logScrollTimer;

        public AutoPlaceSectionsWindow(AutoPlaceSectionsViewModel viewModel)
        {
            InitializeComponent();

            try
            {
                DataContext = viewModel;

                IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
                new WindowInteropHelper(this) { Owner = revitHandle };

                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ShowInTaskbar = false;

                Loaded  += (s, e) => { Activate(); StartLogAutoScroll(); };
                Closing += OnWindowClosing;

                if (viewModel.LogEntries is System.Collections.Specialized.INotifyCollectionChanged notifyCollection)
                    notifyCollection.CollectionChanged += (s, e) => ScrollLogToBottom();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialise window: {ex.Message}",
                    "APUS V330 Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void StartLogAutoScroll()
        {
            _logScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _logScrollTimer.Tick += (s, e) => ScrollLogToBottom();
            _logScrollTimer.Start();
        }

        private void ScrollLogToBottom()
        {
            if (LogListBox?.Items.Count > 0)
            {
                LogListBox.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    try { LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]); }
                    catch { /* ignore */ }
                }));
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _logScrollTimer?.Stop();
            _logScrollTimer = null;

            if (DataContext is AutoPlaceSectionsViewModel vm)
            {
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

                vm.OnWindowClosing();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
