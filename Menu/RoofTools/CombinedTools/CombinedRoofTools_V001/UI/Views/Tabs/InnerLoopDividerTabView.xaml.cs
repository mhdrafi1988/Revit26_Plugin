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
