using Revit26_Plugin.RoofCreationIsolationTest.V001.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace Revit26_Plugin.RoofCreationIsolationTest.V001.UI.Views
{
    /// <summary>
    /// Code-behind for the isolation test window. Modeless, Close-only (no OK/Cancel),
    /// Esc closes — per project dialog convention. Never auto-closes on its own; only
    /// user-initiated Close (button or Esc) ends the window.
    /// </summary>
    public partial class RunTestWindow : Window
    {
        public RunTestWindow(RunTestViewModel viewModel)
        {
            InitializeComponent(); // This will now resolve if XAML exists and is correct
            DataContext = viewModel;

            PreviewKeyDown += RunTestWindow_PreviewKeyDown;
        }

        private void RunTestWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
