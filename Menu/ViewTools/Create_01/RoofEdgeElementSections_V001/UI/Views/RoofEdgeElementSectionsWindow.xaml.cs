using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofEdgeElementSections.V001
{
    public partial class RoofEdgeElementSectionsWindow : Window
    {
        private readonly RoofEdgeElementSectionsViewModel _viewModel;
        private readonly UIApplication _uiApp;

        public RoofEdgeElementSectionsWindow(RoofEdgeElementSectionsViewModel viewModel, UIApplication uiApp, IntPtr revitMainWindowHandle)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _uiApp = uiApp;
            DataContext = _viewModel;

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            helper.Owner = revitMainWindowHandle;

            _viewModel.CopyLogsRequested = OnCopyLogsRequested;
            _viewModel.ExportLogsRequested = OnExportLogsRequested;
            _viewModel.RequestOpenViewsIfNeeded = OnRequestOpenViewsIfNeeded;
            _viewModel.HideWindow = Hide;
            _viewModel.ShowWindow = Show;

            Closing += RoofEdgeElementSectionsWindow_Closing;
        }

        private void RoofEdgeElementSectionsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.OnWindowClosing();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
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
                    Title = "Select folder to save RoofEdgeElementSections logs"
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
                        "Roof Edge Element Sections",
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
