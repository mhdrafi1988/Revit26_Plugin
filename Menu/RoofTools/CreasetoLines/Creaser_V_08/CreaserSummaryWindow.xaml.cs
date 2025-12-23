using System.Windows;

namespace Revit26_Plugin.Creaser_V08.Commands.UI
{
    public partial class CreaserSummaryWindow : Window
    {
        public CreaserSummaryWindow()
        {
            InitializeComponent();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
