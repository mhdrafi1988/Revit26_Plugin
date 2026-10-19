using Revit26_Plugin.DeleteWorkset.V001.UI.ViewModels;
using System.Windows;

namespace Revit26_Plugin.DeleteWorkset.V001.UI.Views
{
    public partial class DeleteWorksetWindow : Window
    {
        public DeleteWorksetWindow(DeleteWorksetViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
