using System.Windows;

namespace Revit22_Plugin.RRLPV3.Views
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
