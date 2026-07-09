using System.Windows;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.ViewModels;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V001.Views
{
    public partial class RoofDrainCalloutPlacingWindow : Window
    {
        public RoofDrainCalloutPlacingWindow(RoofDrainCalloutPlacingViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is RoofDrainCalloutPlacingViewModel vm)
                vm.CopySelectedLogs(LogListBox.SelectedItems);
        }
    }
}
