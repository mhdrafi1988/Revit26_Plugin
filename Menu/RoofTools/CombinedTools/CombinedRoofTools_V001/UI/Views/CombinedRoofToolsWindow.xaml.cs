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
            {
                if (vm.IsRunningAll)
                {
                    MessageBox.Show(this, "Run All is still in progress. Please wait for it to finish before closing.",
                        "Combined Roof Tools", MessageBoxButton.OK, MessageBoxImage.Information);
                    e.Cancel = true;
                    return;
                }

                vm.SaveOnClose();
            }
            base.OnClosing(e);
        }
    }
}
