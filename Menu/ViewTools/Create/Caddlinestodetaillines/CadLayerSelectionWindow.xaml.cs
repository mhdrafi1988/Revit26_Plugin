using System.Windows;
using Revit22_Plugin.ImportCadLines.ViewModels;

namespace Revit22_Plugin.ImportCadLines.Views
{
    public partial class CadLayerSelectionWindow : Window
    {
        public CadLayerSelectionViewModel ViewModel => DataContext as CadLayerSelectionViewModel;

        public CadLayerSelectionWindow(CadLayerSelectionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(ViewModel.SelectedLayer))
                DialogResult = true;
            else
                MessageBox.Show("Please select a layer.");
        }
    }
}
