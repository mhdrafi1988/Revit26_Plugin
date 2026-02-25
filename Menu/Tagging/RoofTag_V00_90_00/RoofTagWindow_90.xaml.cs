using Autodesk.Revit.UI;
using System.Windows;

namespace Revit22_Plugin.RoofTag_V90
{
    public partial class RoofTagWindowV3 : Window
    {
        public RoofTagWindowV3(UIApplication uiApp)
        {
            InitializeComponent();

            // Attach ViewModel
            DataContext = new RoofTagViewModelV3(uiApp);
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
