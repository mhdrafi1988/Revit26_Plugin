using System.Windows;

namespace Revit22_Plugin.PDCV1.Views
{
    public partial class RoofLoopAnalyzerWindow : Window
    {
        public RoofLoopAnalyzerWindow()
        {
            InitializeComponent();
        }

        private void DataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // TODO: Implement selection changed logic if needed
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
