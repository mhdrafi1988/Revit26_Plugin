// =======================================================
// File: InnerLoopDividerWindow.xaml.cs
// Location: UI/Views/
// Renamed from RoofLoopAnalyzerWindow (V007).
// Changes vs V007:
//   ADDED CloseButton_Click — needed now that the window is modeless
//   (Show()); IsCancel="True" alone only auto-closes for ShowDialog().
// =======================================================

using Revit26_Plugin.InnerLoopDivider.V009.UI.ViewModels;
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
