// =======================================================
// File: InnerLoopsAndPerpendicularWindow.xaml.cs
// Location: UI/Views/
// Renamed from PerpendicularPointWindow (V003).
// Changes vs V003:
//   ADDED CloseButton_Click — needed now that the window is modeless
//   (Show()); IsCancel="True" alone only auto-closes for ShowDialog().
// =======================================================

using Revit26_Plugin.InnerLoopsAndPerpendicular.V005.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.InnerLoopsAndPerpendicular.V005.UI.Views
{
    public partial class InnerLoopsAndPerpendicularWindow : Window
    {
        public InnerLoopsAndPerpendicularWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void SelectGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is InnerLoopsAndPerpendicularViewModel vm)
                vm.SelectGroupLoops(btn.Tag?.ToString());
        }

        private void ClearGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is InnerLoopsAndPerpendicularViewModel vm)
                vm.ClearGroupLoops(btn.Tag?.ToString());
        }
    }
}
