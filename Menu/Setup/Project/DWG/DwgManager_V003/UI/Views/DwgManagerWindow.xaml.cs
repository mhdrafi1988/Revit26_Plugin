using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Revit26_Plugin_Revised.DwgManager.V003.UI.ViewModels;
using Revit26_Plugin_Revised.Shared.Services;

namespace Revit26_Plugin_Revised.DwgManager.V003.UI.Views
{
    /// <summary>
    /// DWG Manager window; everything is bound to <see cref="DwgManagerViewModel"/>. The code-behind only
    /// sizes and closes the window, keeps the group order while sorting, syncs the active view when the
    /// window is activated, copies log lines and keeps the newest log line in view.
    /// </summary>
    public partial class DwgManagerWindow : Window
    {
        private bool _scrollPending;

        /// <summary>The window's view model.</summary>
        public DwgManagerViewModel ViewModel { get; }

        /// <summary>Creates the window for <paramref name="viewModel"/>.</summary>
        public DwgManagerWindow(DwgManagerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = ViewModel;
            Title = ToolCatalog.DwgManager.Title;

            // Resize limit: never smaller than the default size, at most 130 % of it (capped to the screen).
            MaxWidth = Math.Min(Width * 1.3, SystemParameters.WorkArea.Width);
            MaxHeight = Math.Min(Height * 1.3, SystemParameters.WorkArea.Height);

            ViewModel.LogEntries.CollectionChanged += OnLogChanged;
            Activated += OnActivated;
            Closed += (_, _) =>
            {
                Activated -= OnActivated;
                ViewModel.LogEntries.CollectionChanged -= OnLogChanged;
                ViewModel.Shutdown();
            };
        }

        // Coming back to the window after switching views in Revit refreshes the Active badges and IDs.
        private void OnActivated(object sender, EventArgs e) => ViewModel.SyncActiveView();

        // Sorting stays inside the Linked/Imported → view-type groups; a third click returns to the default order.
        private void DwgGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var column = e.Column;

            ListSortDirection? next = column.SortDirection switch
            {
                null => ListSortDirection.Ascending,
                ListSortDirection.Ascending => ListSortDirection.Descending,
                _ => null
            };

            foreach (var other in DwgGrid.Columns)
                if (!ReferenceEquals(other, column)) other.SortDirection = null;

            column.SortDirection = next;
            ViewModel.ApplySort(next.HasValue ? column.SortMemberPath : null, next ?? ListSortDirection.Ascending);
        }

        // Scrolling inside a CollectionChanged handler makes WPF throw "ItemsControl is inconsistent with
        // its items source" when many lines arrive at once, so the scroll is deferred and coalesced.
        private void OnLogChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || _scrollPending) return;

            _scrollPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _scrollPending = false;
                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            }));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void CopyAllLog_Click(object sender, RoutedEventArgs e) => LogClipboardService.CopyAll(LogList.Items);

        private void CopySelectedLog_Click(object sender, RoutedEventArgs e) => LogClipboardService.CopySelected(LogList.SelectedItems);
    }
}
