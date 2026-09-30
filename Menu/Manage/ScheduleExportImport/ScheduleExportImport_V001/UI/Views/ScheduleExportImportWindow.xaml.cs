using System;
using System.Collections.Specialized;
using System.Windows;
using Revit26_Plugin.ScheduleExportImport.V001.UI.ViewModels;

namespace Revit26_Plugin.ScheduleExportImport.V001.UI.Views
{
    public partial class ScheduleExportImportWindow : Window
    {
        private readonly ScheduleExportImportViewModel _viewModel;
        private ImportPreviewWindow _preview;

        public ScheduleExportImportWindow(ScheduleExportImportViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;

            _viewModel.PreviewRequested += ShowPreview;
            _viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
            Closed += OnClosed;
        }

        private void LogEntries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }

        private void ShowPreview()
        {
            if (_preview == null)
            {
                _preview = new ImportPreviewWindow(_viewModel) { Owner = this };
                _preview.Closed += (_, _) => _preview = null;
                _preview.Show();
            }
            else
            {
                if (_preview.WindowState == WindowState.Minimized) _preview.WindowState = WindowState.Normal;
                _preview.Activate();
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _viewModel.PreviewRequested -= ShowPreview;
            _viewModel.LogEntries.CollectionChanged -= LogEntries_CollectionChanged;
            _preview?.Close();
        }
    }
}
