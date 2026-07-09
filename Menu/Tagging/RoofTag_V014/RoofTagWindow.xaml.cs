using Autodesk.Revit.UI;
using System.Windows;

namespace Revit26_Plugin.RoofTag_V014
{
    public partial class RoofTagWindow : Window
    {
        public RoofTagWindow(UIApplication uiApp)
        {
            InitializeComponent();
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
