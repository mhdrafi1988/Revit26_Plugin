using Revit26_Plugin.InnerLoopDivider.V007.ViewModels;
using System.Windows.Controls;
using System.Windows;

namespace Revit26_Plugin.InnerLoopDivider.V007.Views
{
    public partial class RoofLoopAnalyzerWindow
    {
        public RoofLoopAnalyzerWindow()
        {
            InitializeComponent();
        }

        private void SelectGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is RoofLoopAnalyzerViewModel vm)
                vm.SelectGroupLoops(btn.Tag?.ToString());
        }

        private void ClearGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is RoofLoopAnalyzerViewModel vm)
                vm.ClearGroupLoops(btn.Tag?.ToString());
        }
    }
}
