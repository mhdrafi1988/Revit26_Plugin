using System.Windows;
using Revit26_Plugin.WorksetRenamer.V004.ViewModels;

namespace Revit26_Plugin.WorksetRenamer.V004.Views
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
