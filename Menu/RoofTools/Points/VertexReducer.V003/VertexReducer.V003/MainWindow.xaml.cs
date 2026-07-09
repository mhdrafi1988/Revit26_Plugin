using System.Windows;
using Revit26_Plugin.RoofEdgeVertexReducer.V003.ViewModels;

namespace Revit26_Plugin.RoofEdgeVertexReducer.V003.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void CopySelectedLogs_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CopySelectedLogs(LogList.SelectedItems);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
