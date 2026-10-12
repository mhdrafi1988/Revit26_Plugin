using Autodesk.Revit.UI;
using System.Windows;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.RoofTag.V018
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

        private void CopySelectedLog_Click(object sender, System.Windows.RoutedEventArgs e) => LogClipboardService.CopySelected(LogList.SelectedItems);
    }
}
