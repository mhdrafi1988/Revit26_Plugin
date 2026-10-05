using System.Windows;
using Revit26_Plugin.WorksetRenamer.V005.ViewModels;

namespace Revit26_Plugin.WorksetRenamer.V005.Views
{
    public partial class WorksetRenamerView : Window
    {
        public WorksetRenamerView(WorksetRenamerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
