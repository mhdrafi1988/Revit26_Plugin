using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    public partial class ViewSheetPlacerView : Window
    {
        private readonly ViewSheetPlacerViewModel _vm;

        public ViewSheetPlacerView(ViewSheetPlacerViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            _vm.PropertyChanged += Vm_PropertyChanged;
            _vm.Logs.CollectionChanged += Logs_CollectionChanged;

            Closed += (_, __) =>
            {
                _vm.PropertyChanged -= Vm_PropertyChanged;
                _vm.Logs.CollectionChanged -= Logs_CollectionChanged;
                Mouse.OverrideCursor = null;
                _vm.OnClosing();
            };
        }

        // App-wide wait cursor while a run is in progress (covers child controls).
        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewSheetPlacerViewModel.IsRunning))
                Mouse.OverrideCursor = _vm.IsRunning ? Cursors.Wait : null;
        }

        // Keep the newest log line visible.
        private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }

        private void LogList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.SelectedLogs.Clear();
            foreach (var item in LogList.SelectedItems)
                if (item is LogEntry le) _vm.SelectedLogs.Add(le);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
