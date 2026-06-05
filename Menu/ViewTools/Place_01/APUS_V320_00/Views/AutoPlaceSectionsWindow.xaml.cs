// File: AutoPlaceSectionsWindow.xaml.cs
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V320.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Revit26_Plugin.APUS_V320.Views
{
    public partial class AutoPlaceSectionsWindow : Window
    {
        private DispatcherTimer _logScrollTimer;

        // Guard flag: prevents SelectionChanged from re-entering itself
        // when we programmatically change IsSelected inside the handler.
        private bool _suppressSelectionSync = false;

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

                Loaded += (s, e) =>
                {
                    Activate();
                    StartLogAutoScroll();
                };

                Closing += OnWindowClosing;

                if (viewModel.LogEntries is System.Collections.Specialized.INotifyCollectionChanged nc)
                    nc.CollectionChanged += (s, e) => ScrollLogToBottom();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize window: {ex.Message}",
                    "APUS Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        // ── Checkbox click — single-click toggle ──────────────────────
        // The CheckBox inside the DataGridTemplateColumn handles its own
        // two-way binding, so this handler only needs to keep the DataGrid
        // row highlight in sync and notify the ViewModel counter.
        private void SectionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AutoPlaceSectionsViewModel vm) return;

            // Prevent the SelectionChanged handler from fighting this click
            _suppressSelectionSync = true;
            try
            {
                // Notify ViewModel that the selected count may have changed
                vm.NotifySelectionCountChanged();
            }
            finally
            {
                _suppressSelectionSync = false;
            }

            e.Handled = true; // stop bubbling so row highlight doesn't interfere
        }

        // ── DataGrid row selection → IsSelected sync ──────────────────
        // When the user clicks a row (not the checkbox), we sync the
        // DataGrid's highlighted rows back to IsSelected on the item.
        // This enables Shift+click range selection and Ctrl+click multi-pick.
        private void SectionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSync) return;
            if (DataContext is not AutoPlaceSectionsViewModel vm) return;

            _suppressSelectionSync = true;
            try
            {
                // Items added to DataGrid selection → tick their checkbox
                foreach (var item in e.AddedItems.OfType<SectionItemViewModel>())
                    item.IsSelected = true;

                // Items removed from DataGrid selection → untick their checkbox
                foreach (var item in e.RemovedItems.OfType<SectionItemViewModel>())
                    item.IsSelected = false;

                vm.NotifySelectionCountChanged();
            }
            finally
            {
                _suppressSelectionSync = false;
            }
        }

        // ── Log helpers ───────────────────────────────────────────────
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
                    catch { }
                }));
            }
        }

        // ── Window lifecycle ──────────────────────────────────────────
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _logScrollTimer?.Stop();
            _logScrollTimer = null;

            if (DataContext is AutoPlaceSectionsViewModel vm)
            {
                if (vm.IsProcessing)
                {
                    var r = MessageBox.Show(
                        "Placement is in progress. Are you sure you want to cancel and close?",
                        "Confirm Close", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (r == MessageBoxResult.Yes)
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
