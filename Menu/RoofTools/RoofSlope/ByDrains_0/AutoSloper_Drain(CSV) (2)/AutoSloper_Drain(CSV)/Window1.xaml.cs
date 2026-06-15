using Revit26_Plugin.Asd_19.ViewModels;
using System.Windows;

namespace Revit26_Plugin.Asd_19.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Hook up close action from ViewModel
            viewModel.CloseWindow = () => this.Close();

            // Optional: cleanup logic
            this.Closing += (s, e) =>
            {
                // Insert any cleanup here if needed
            };
        }

        // This handles the ✕ close button in the UI
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}