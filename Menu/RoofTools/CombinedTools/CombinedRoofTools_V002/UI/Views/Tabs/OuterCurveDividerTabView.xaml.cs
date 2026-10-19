using Revit26_Plugin.OuterCurveDivider.V004.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.CombinedRoofTools.V002.UI.Views.Tabs
{
    public partial class OuterCurveDividerTabView : UserControl
    {
        public OuterCurveDividerTabView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // Auto-scroll the log to the newest entry as rows are added.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is CurveDividerViewModel vm)
                vm.LogEntries.CollectionChanged += (s, a) => ScrollLogToEnd();
        }

        private void ScrollLogToEnd()
        {
            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }

        private void CopyAllLog_Click(object sender, System.Windows.RoutedEventArgs e) => LogClipboardService.CopyAll(LogList.Items);

        private void CopySelectedLog_Click(object sender, System.Windows.RoutedEventArgs e) => LogClipboardService.CopySelected(LogList.SelectedItems);
    }
}
