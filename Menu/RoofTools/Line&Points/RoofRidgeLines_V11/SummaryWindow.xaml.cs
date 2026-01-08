using System.Windows;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Views
{
    public partial class SummaryWindow : Window
    {
        public SummaryWindow()
        {
            InitializeComponent();
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
