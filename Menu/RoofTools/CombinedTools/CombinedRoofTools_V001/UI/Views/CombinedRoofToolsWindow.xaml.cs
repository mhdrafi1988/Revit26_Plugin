using Revit26_Plugin.CombinedRoofTools.V001.UI.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Revit26_Plugin.CombinedRoofTools.V001.UI.Views
{
    public partial class CombinedRoofToolsWindow : Window
    {
        public CombinedRoofToolsWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is CombinedRoofToolsViewModel vm)
                vm.SaveOnClose();
            base.OnClosing(e);
        }
    }
}
