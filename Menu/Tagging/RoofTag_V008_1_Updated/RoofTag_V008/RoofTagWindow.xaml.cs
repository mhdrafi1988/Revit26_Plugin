using Autodesk.Revit.UI;
using System.Windows;

namespace Revit26_Plugin.RoofTag_V008
{
    /// <summary>
    /// Interaction logic for RoofTagWindow.xaml
    /// V008: Rearranged UI (summary above log), user-configurable clustering tolerance
    /// </summary>
    public partial class RoofTagWindow : Window
    {
        private readonly UIApplication _uiApp;

        public RoofTagWindow(UIApplication uiApp)
        {
            _uiApp = uiApp;
            DataContext = new RoofTagViewModel(uiApp);
            InitializeComponent();
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
