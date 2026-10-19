using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using Revit26_Plugin.DwgStatistics.V005.Core.Models;
using Revit26_Plugin.DwgStatistics.V005.UI.ViewModels;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.DwgStatistics.V005.UI.Views
{
    /// <summary>
    /// DWG Statistics window; everything is bound to <see cref="DwgStatisticsViewModel"/>. The code-behind
    /// only sizes and closes the window, starts the first scan once the window is on screen, copies log
    /// lines and keeps the newest log line in view.
    /// </summary>
    public partial class DwgStatisticsWindow : Window
    {
        private bool _scrollPending;
        private bool _firstScanQueued;

        /// <summary>The window's view model.</summary>
        public DwgStatisticsViewModel ViewModel { get; }

        /// <summary>Creates the window for <paramref name="viewModel"/>.</summary>
        public DwgStatisticsWindow(DwgStatisticsViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = ViewModel;
            Title = DwgStatisticsTool.Label;

            // Resize limit: never smaller than the minimum, at most 130 % of the default (capped to the screen).
            MaxWidth = Math.Min(Width * 1.3, SystemParameters.WorkArea.Width);
            MaxHeight = Math.Min(Height * 1.3, SystemParameters.WorkArea.Height);

            ViewModel.LogEntries.CollectionChanged += OnLogChanged;
            ContentRendered += OnContentRendered;
            Closed += (_, _) =>
            {
                ContentRendered -= OnContentRendered;
                ViewModel.LogEntries.CollectionChanged -= OnLogChanged;
                ViewModel.Shutdown();
            };
        }

        // First scan after the window is drawn, so "Scanning…" is visible while the deep scan runs.
        private void OnContentRendered(object sender, EventArgs e)
        {
            if (_firstScanQueued) return;
            _firstScanQueued = true;
            if (ViewModel.RefreshCommand.CanExecute(null)) ViewModel.RefreshCommand.Execute(null);
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
