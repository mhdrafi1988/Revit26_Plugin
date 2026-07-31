using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeSections.V001
{
    public partial class RoofEdgeSectionsWindow : Window
    {
        private readonly RoofEdgeSectionsViewModel _viewModel;
        private readonly UIApplication _uiApp;

        public RoofEdgeSectionsWindow(RoofEdgeSectionsViewModel viewModel, UIApplication uiApp, IntPtr revitMainWindowHandle)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _uiApp = uiApp;
            DataContext = _viewModel;

            // Per convention: WindowInteropHelper with the Revit main window handle,
            // never Application.Current.MainWindow.
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            helper.Owner = revitMainWindowHandle;

            _viewModel.CopyLogsRequested = OnCopyLogsRequested;
            _viewModel.ExportLogsRequested = OnExportLogsRequested;
            _viewModel.RequestOpenViewsIfNeeded = OnRequestOpenViewsIfNeeded;

            Closing += RoofEdgeSectionsWindow_Closing;
        }

        private void RoofEdgeSectionsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.OnWindowClosing();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Blocks the checkbox click from also triggering DataGrid row selection,
        // per DataGrid checkbox-column convention.
        private void CheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false; // allow the checkbox itself to toggle; only suppress row-select cascade
            if (sender is FrameworkElement fe)
            {
                var row = FindParent<System.Windows.Controls.DataGridRow>(fe);
                if (row != null)
                    row.IsSelected = false;
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            return parent as T;
        }

        private void OnCopyLogsRequested(List<LogEntry> entries)
        {
            if (entries.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var e in entries)
                sb.AppendLine(e.ToString());
            Clipboard.SetText(sb.ToString());
        }

        private void OnExportLogsRequested(List<LogEntry> entries, string lastFolder)
        {
            if (entries.Count == 0) return;

            string folder = lastFolder;
            if (string.IsNullOrWhiteSpace(folder))
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select folder to save RoofEdgeSections logs"
                };
                if (dialog.ShowDialog() != true)
                    return;
                folder = dialog.FolderName;
            }

            LogExportHelper.Export(entries, folder);
        }

        private void OnRequestOpenViewsIfNeeded(List<ElementId> createdViewIds, string openViewsMode)
        {
            if (createdViewIds.Count == 0) return;

            bool shouldOpen = openViewsMode switch
            {
                "OpenAll" => true,
                "DontOpen" => false,
                _ => System.Windows.MessageBox.Show(
                        $"{createdViewIds.Count} section view(s) were created. Open them now?",
                        "Roof Edge Sections",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes
            };

            if (!shouldOpen) return;

            Document doc = _uiApp.ActiveUIDocument.Document;
            foreach (ElementId id in createdViewIds)
            {
                if (doc.GetElement(id) is View view)
                {
                    try { _uiApp.ActiveUIDocument.ActiveView = view; }
                    catch { /* best-effort — some views may not be openable in the current context */ }
                }
            }
        }
    }
}
