// =======================================================
// File: InnerLoopDividerTabView.xaml.cs
// Location: CombinedRoofTools_V001/UI/Views/Tabs/
// UserControl conversion of InnerLoopDivider_V009's InnerLoopDividerWindow
// for embedding as a tab inside the Combined Roof Tools window.
//
// CloseButton_Click was removed — the combined window supplies its own
// shared Close button, so this tab no longer needs one. DataContext is
// not set here; the hosting window binds it externally per tab.
// =======================================================

using Revit26_Plugin.InnerLoopDivider.V009.UI.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.CombinedRoofTools.V001.UI.Views.Tabs
{
    public partial class InnerLoopDividerTabView : UserControl
    {
        public InnerLoopDividerTabView()
        {
            InitializeComponent();
        }

        // Column-header sort that keeps rows grouped by shape (see InnerLoopDividerWindow).
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
