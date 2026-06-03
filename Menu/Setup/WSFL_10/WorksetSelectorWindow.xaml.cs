using MahApps.Metro.Controls;
using Revit26_Plugin.WSFL_010.ViewModels;

namespace Revit26_Plugin.WSFL_010.Views
{
    public partial class WorksetSelectorWindow : MetroWindow
    {
        public WorksetSelectorWindow(WorksetsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += Close;
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }
}
