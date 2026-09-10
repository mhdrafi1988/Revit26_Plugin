// =======================================================
// File: InnerLoopsAndPerpendicularTabView.xaml.cs
// Location: CombinedRoofTools_V001/UI/Views/Tabs/
//
// Embeddable UserControl extraction of InnerLoopsAndPerpendicularWindow
// (Menu/RoofTools/Points/InnerLoopsAndPerpendicular_V005) for hosting as
// a tab inside the Combined Roof Tools window. DataContext is supplied
// externally by the combined window's binding — this view does not set
// its own DataContext.
//
// Changes vs the standalone Window code-behind:
//   REMOVED CloseButton_Click — the combined window provides one shared
//   Close button for the whole dialog.
// =======================================================

using Revit26_Plugin.InnerLoopsAndPerpendicular.V005.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.CombinedRoofTools.V001.UI.Views.Tabs
{
    public partial class InnerLoopsAndPerpendicularTabView : UserControl
    {
        public InnerLoopsAndPerpendicularTabView()
        {
            InitializeComponent();
        }

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
