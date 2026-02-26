using Autodesk.Revit.UI;
using Revit26_Plugin.RoofTag_V73.ViewModels;
using System.Windows;

namespace Revit26_Plugin.RoofTag_V73.Views
{
    /// <summary>
    /// Interaction logic for RoofTagWindow.xaml
    /// UI-only class. No business logic allowed.
    /// </summary>
    public partial class RoofTagWindow : Window
    {
        public RoofTagWindow(UIApplication uiApp)
        {
            InitializeComponent();

            // ViewModel wiring only (MVVM-compliant)
            DataContext = new RoofTagViewModel(uiApp);
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
