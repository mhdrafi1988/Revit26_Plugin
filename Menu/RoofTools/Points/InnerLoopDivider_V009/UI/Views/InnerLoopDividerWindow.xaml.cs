// =======================================================
// File: InnerLoopDividerWindow.xaml.cs
// Location: UI/Views/
// Renamed from RoofLoopAnalyzerWindow (V007).
// Changes vs V007:
//   ADDED CloseButton_Click — needed now that the window is modeless
//   (Show()); IsCancel="True" alone only auto-closes for ShowDialog().
// =======================================================

using Revit26_Plugin.InnerLoopDivider.V009.UI.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.InnerLoopDivider.V009.UI.Views
{
    public partial class InnerLoopDividerWindow : Window
    {
        public InnerLoopDividerWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        // Column-header sort that keeps rows grouped by shape: the DataGrid's built-in
        // sort would replace the group-order sort and split the groups apart.
        private void LoopsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (DataContext is not InnerLoopDividerViewModel vm ||
                string.IsNullOrEmpty(e.Column.SortMemberPath))
                return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            vm.SortBy(e.Column.SortMemberPath, direction);

            foreach (var col in ((DataGrid)sender).Columns)
                col.SortDirection = null;
            e.Column.SortDirection = direction;
            e.Handled = true;
        }

        private void SelectGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is InnerLoopDividerViewModel vm)
                vm.SelectGroupLoops(btn.Tag?.ToString());
        }

        private void ClearGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is InnerLoopDividerViewModel vm)
                vm.ClearGroupLoops(btn.Tag?.ToString());
        }
    }
}
