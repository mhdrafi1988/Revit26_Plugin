using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofPointComparison.V001.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace Revit26_Plugin.RoofPointComparison.V001.UI.Views
{
    public partial class RoofComparisonWindow : Window
    {
        public RoofComparisonWindow(UIDocument uidoc, UIApplication app, ElementId roofAId, ElementId roofBId)
        {
            InitializeComponent();

            DataContext = new RoofComparisonViewModel(uidoc, app, roofAId, roofBId);
            Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
