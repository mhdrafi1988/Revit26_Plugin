using Revit26_Plugin.InnerLoopsAndPerpendicular.V003.ViewModels;
using System.Windows.Controls;
using System.Windows;

namespace Revit26_Plugin.InnerLoopsAndPerpendicular.V003.Views
{
    public partial class PerpendicularPointWindow
    {
        public PerpendicularPointWindow()
        {
            InitializeComponent();
        }

        private void SelectGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is PerpendicularPointViewModel vm)
                vm.SelectGroupLoops(btn.Tag?.ToString());
        }

        private void ClearGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is PerpendicularPointViewModel vm)
                vm.ClearGroupLoops(btn.Tag?.ToString());
        }
    }
}
