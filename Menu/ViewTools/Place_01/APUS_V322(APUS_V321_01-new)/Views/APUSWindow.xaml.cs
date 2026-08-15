// File: AutoPlaceSectionsWindow.xaml.cs
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS.V322.Models;
using Revit26_Plugin.APUS.V322.Services;
using Revit26_Plugin.APUS.V322.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Revit26_Plugin.APUS.V322.Views
{
    public partial class AutoPlaceSectionsWindow : Window
    {
        private DispatcherTimer _logScrollTimer;
        private bool _suppressSelectionSync = false;

        /// <summary>
        /// V321 fix: V320 set the window owner from
        /// Process.GetCurrentProcess().MainWindowHandle, which is not the
        /// pattern the suite requires. The owner must come from Revit's own
        /// UIApplication.MainWindowHandle, passed in from the Command.
        /// </summary>
        public AutoPlaceSectionsWindow(AutoPlaceSectionsViewModel viewModel, IntPtr revitMainWindowHandle)
        {
            InitializeComponent();

            try
            {
                DataContext = viewModel;

                new WindowInteropHelper(this) { Owner = revitMainWindowHandle };

                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ShowInTaskbar = false;

                viewModel.PendingConfirmation += OnPendingConfirmation;

                Loaded += (s, e) =>
                {
                    Activate();
                    StartLogAutoScroll();
                    UpdateFilterButtonVisuals();
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

        // ---------------- Pre-placement confirmation ----------------
        // Answers the ViewModel's PendingConfirmation event synchronously via
        // MessageBox, per Rafi's approval of the event-based pattern (keeps
        // MessageBox out of the ViewModel for testability).
        private void OnPendingConfirmation(object sender, PlacementConfirmationEventArgs e)
        {
            var result = MessageBox.Show(
                $"About to place {e.SectionCount} section(s) across an estimated {e.EstimatedSheetCount} sheet(s). Proceed?",
                "Confirm Placement",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            e.Confirmed = result == MessageBoxResult.Yes;
        }

        // ---------------- Sortable grid headers ----------------
        private void SortByName_Click(object sender, RoutedEventArgs e) => Sort(SectionGridSortColumn.Name);
        private void SortBySize_Click(object sender, RoutedEventArgs e) => Sort(SectionGridSortColumn.Size);
        private void SortByPlaced_Click(object sender, RoutedEventArgs e) => Sort(SectionGridSortColumn.Placed);
        private void SortByDetailNumber_Click(object sender, RoutedEventArgs e) => Sort(SectionGridSortColumn.DetailNumber);

        private void Sort(SectionGridSortColumn column)
        {
            if (DataContext is AutoPlaceSectionsViewModel vm)
                vm.SetSortColumn(column);
        }

        // ---------------- All / Placed / Unplaced filter ----------------
        private void FilterAll_Click(object sender, RoutedEventArgs e) => SetFilter(PlacementFilterState.All);
        private void FilterPlaced_Click(object sender, RoutedEventArgs e) => SetFilter(PlacementFilterState.PlacedOnly);
        private void FilterUnplaced_Click(object sender, RoutedEventArgs e) => SetFilter(PlacementFilterState.UnplacedOnly);

        private void SetFilter(PlacementFilterState state)
        {
            if (DataContext is AutoPlaceSectionsViewModel vm)
            {
                vm.PlacementFilter = state;
                UpdateFilterButtonVisuals();
            }
        }

        private void UpdateFilterButtonVisuals()
        {
            if (DataContext is not AutoPlaceSectionsViewModel vm) return;

            var navy = (Brush)FindResource("BrushNavyPrimary");
            var textPrimary = (Brush)FindResource("BrushTextPrimary");

            FilterAllButton.Background = vm.PlacementFilter == PlacementFilterState.All ? navy : Brushes.Transparent;
            FilterAllButton.Foreground = vm.PlacementFilter == PlacementFilterState.All ? Brushes.White : textPrimary;

            FilterPlacedButton.Background = vm.PlacementFilter == PlacementFilterState.PlacedOnly ? navy : Brushes.Transparent;
            FilterPlacedButton.Foreground = vm.PlacementFilter == PlacementFilterState.PlacedOnly ? Brushes.White : textPrimary;

            FilterUnplacedButton.Background = vm.PlacementFilter == PlacementFilterState.UnplacedOnly ? navy : Brushes.Transparent;
            FilterUnplacedButton.Foreground = vm.PlacementFilter == PlacementFilterState.UnplacedOnly ? Brushes.White : textPrimary;
        }

        // ---------------- Checkbox / row selection sync ----------------
        private void SectionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AutoPlaceSectionsViewModel vm) return;

            _suppressSelectionSync = true;
            try
            {
                vm.NotifySelectionCountChanged();
            }
            finally
            {
                _suppressSelectionSync = false;
            }

            e.Handled = true;
        }

        private void SectionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSync) return;
            if (DataContext is not AutoPlaceSectionsViewModel vm) return;

            _suppressSelectionSync = true;
            try
            {
                foreach (var item in e.AddedItems.OfType<SectionItemViewModel>())
                {
                    if (!item.IsPlaced) item.IsSelected = true;
                }

                foreach (var item in e.RemovedItems.OfType<SectionItemViewModel>())
                    item.IsSelected = false;

                vm.NotifySelectionCountChanged();
            }
            finally
            {
                _suppressSelectionSync = false;
            }
        }

        // ---------------- Log helpers ----------------
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
                    catch { /* window may be closing */ }
                }));
            }
        }

        // ---------------- Window lifecycle ----------------
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _logScrollTimer?.Stop();
            _logScrollTimer = null;

            if (DataContext is AutoPlaceSectionsViewModel vm)
            {
                vm.PendingConfirmation -= OnPendingConfirmation;

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
