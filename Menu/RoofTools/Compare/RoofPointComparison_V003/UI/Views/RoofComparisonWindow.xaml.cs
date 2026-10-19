using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofPointComparison.V003.UI.ViewModels;
using System.Windows;
using System.Windows.Input;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.RoofPointComparison.V003.UI.Views
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

        private void CopyAllLog_Click(object sender, System.Windows.RoutedEventArgs e) => LogClipboardService.CopyAll(LogListBox.Items);

        private void CopySelectedLog_Click(object sender, System.Windows.RoutedEventArgs e) => LogClipboardService.CopySelected(LogListBox.SelectedItems);
    }
}
