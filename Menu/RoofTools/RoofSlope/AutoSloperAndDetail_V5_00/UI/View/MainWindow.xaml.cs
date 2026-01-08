using System.Windows;
using Revit26_Plugin.V5_00.UI.ViewModels;

namespace Revit26_Plugin.V5_00.UI.Views
{
    public partial class MainWindow : Window
    {
        private bool _hasExecuted = false;

        public MainWindow(RoofSlopeMainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.CloseWindow = Close;
        }

        // 🔴 GUARANTEED ONE-TIME EXECUTION
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasExecuted)
                return;

            _hasExecuted = true;

            // 🔴 DISABLE FIRST (CRITICAL)
            ApplyButton.IsEnabled = false;

            // 🔴 EXECUTE VIEWMODEL LOGIC
            if (DataContext is RoofSlopeMainViewModel vm)
            {
                vm.ApplySlopesFromUI();
            }
        }
    }
}
